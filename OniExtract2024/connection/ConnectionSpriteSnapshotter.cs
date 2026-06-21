// Camera-snapshot rendering adapted from the GPL-2.0 project Sgt_Imalas-Oni-Mods
// (AnimExportTool/AETE_KbacSnapShotter.cs), itself courtesy of Aki
// (https://github.com/aki-art/ONI-Mods). Adapted for OniExtract2024 to render the 16
// utility connection states to PNG.
//
// Difference from the original: OniExtract2024 references the stock (non-publicized)
// Assembly-CSharp, so KBatchedAnimController.batch and KAnimBatchManager.activeBatchSets
// are not directly accessible. We reach them through HarmonyLib.Traverse.

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace OniExtract2024.connection
{
    /// <summary>
    /// Attached to a temporary, freshly-created utility building. Plays each of the 16
    /// connection-state animations and snapshots the kanim into one PNG per bitmask,
    /// then destroys the host GameObject. Driven inline by ExportConnectionSprites'
    /// coroutine (no OnSpawn timing dependency).
    /// </summary>
    public class ConnectionSpriteSnapshotter : KMonoBehaviour
    {
        private Camera snapshotCamera;
        private RenderTexture targetTexture;
        private static readonly int DrawLayer = 30;

        public IEnumerator ExportThenDestroy()
        {
            // Give the freshly-created building a frame (and a moment) to initialise its
            // kanim controller and register its batch with the KAnimBatchManager.
            yield return new WaitForSecondsRealtime(0.1f);

            var visualizer = GetComponent<KAnimGraphTileVisualizer>();
            var kbac = GetComponent<KBatchedAnimController>();
            string prefabId = GetComponent<KPrefabID>().PrefabTag.Name;

            if (visualizer != null && visualizer.connectionManager != null && kbac != null)
            {
                yield return ExportStates(prefabId, visualizer.connectionManager, kbac);
            }
            else
            {
                Debug.LogWarning("OniExtract: " + prefabId + " has no usable KAnimGraphTileVisualizer/connectionManager; skipped.");
            }

            Util.KDestroyGameObject(gameObject);
        }

        private IEnumerator ExportStates(string prefabId, IUtilityNetworkMgr mgr, KBatchedAnimController kbac)
        {
            string dir = ExportConnectionSprites.OutputDir(prefabId);
            Directory.CreateDirectory(dir);

            // UtilityConnections is Left=1, Right=2, Up=4, Down=8 - identical to the
            // website's required bitmask encoding, so i maps straight to the filename.
            for (int i = 0; i <= 15; i++)
            {
                string animation = mgr.GetVisualizerString((UtilityConnections)i);
                if (!string.IsNullOrEmpty(animation) && kbac.HasAnimation(animation))
                {
                    kbac.Play(animation);
                }
                yield return null;
                SnapShot(Path.Combine(dir, i + ".png"));
            }
        }

        private void SnapShot(string path)
        {
            if (snapshotCamera != null)
            {
                Destroy(snapshotCamera.gameObject);
                snapshotCamera = null;
            }
            InitCamera();

            KAnimBatchManager.Instance().UpdateActiveArea(new Vector2I(-9999, -9999), new Vector2I(9999, 9999));
            KAnimBatchManager.Instance().UpdateDirty(Time.frameCount);
            CameraController.Instance.baseCamera.enabled = false;
            snapshotCamera.enabled = true;

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = targetTexture;

            var kbacs = gameObject.GetComponentsInChildren<KBatchedAnimController>()
                .OrderBy(k => k.transform.position.z);
            foreach (var k in kbacs)
            {
                RenderBatch(GetBatch(k), snapshotCamera, DrawLayer);
            }

            snapshotCamera.Render();
            Texture2D tex = new Texture2D(targetTexture.width, targetTexture.height);
            tex.ReadPixels(new Rect(0, 0, targetTexture.width, targetTexture.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Destroy(tex);

            RenderTexture.active = previous;
            snapshotCamera.enabled = false;
            Destroy(snapshotCamera.gameObject);
            snapshotCamera = null;
            CameraController.Instance.baseCamera.enabled = true;
        }

        private void InitCamera()
        {
            var building = GetComponent<Building>();
            int widthInt = building != null ? building.Def.WidthInCells : 1;
            int heightInt = building != null ? building.Def.HeightInCells : 1;

            const float pixelsPerUnit = 100f;
            const float paddingPx = 100f;
            int textureWidth = Mathf.CeilToInt(widthInt * pixelsPerUnit + 2 * paddingPx);
            int textureHeight = Mathf.CeilToInt(heightInt * pixelsPerUnit + 2 * paddingPx);

            targetTexture = new RenderTexture(textureWidth, textureHeight, 24);
            targetTexture.Create();

            var reference = CameraController.Instance.baseCamera;
            snapshotCamera = CameraController.CloneCamera(reference, "OniExtract_ConnectionCam");
            snapshotCamera.transform.parent = reference.transform.parent;
            snapshotCamera.targetTexture = targetTexture;
            snapshotCamera.enabled = false;
            snapshotCamera.backgroundColor = Color.clear;
            snapshotCamera.cullingMask = 1 << DrawLayer;

            var pos = transform.GetPosition();
            pos.z = -100f;
            pos.y += heightInt / 2f;
            if (widthInt % 2 == 0)
                pos.x += 0.5f;
            snapshotCamera.transform.position = pos;
            snapshotCamera.orthographicSize = textureHeight / (2f * pixelsPerUnit);
            snapshotCamera.aspect = (float)textureWidth / textureHeight;
        }

        // --- reflection shims for stock (non-publicized) Assembly-CSharp -------------

        private static KAnimBatch GetBatch(KBatchedAnimController kbac)
        {
            return Traverse.Create(kbac).Field("batch").GetValue<KAnimBatch>();
        }

        private static List<BatchSet> GetActiveBatchSets()
        {
            return Traverse.Create(KAnimBatchManager.Instance()).Field("activeBatchSets").GetValue<List<BatchSet>>();
        }

        private static void RenderBatch(KAnimBatch batch, Camera camera, int layerOverride)
        {
            if (batch == null || batch.size == 0 || !batch.active || batch.materialType == KAnimBatchGroup.MaterialType.UI)
                return;

            BatchSet owningSet = null;
            var sets = GetActiveBatchSets();
            if (sets != null)
            {
                foreach (var set in sets)
                {
                    for (int i = 0; i < set.batchCount; i++)
                    {
                        if (set.GetBatch(i) == batch)
                        {
                            owningSet = set;
                            break;
                        }
                    }
                    if (owningSet != null)
                        break;
                }
            }
            if (owningSet == null)
                return;

            float zNudge = 0.01f / (1 + batch.id % 256);
            Vector3 drawPos = Vector3.zero;
            drawPos.z = batch.position.z + zNudge;
            int layer = layerOverride >= 0 ? layerOverride : batch.layer;

            Graphics.DrawMesh(owningSet.group.mesh, drawPos, Quaternion.identity,
                owningSet.group.GetMaterial(batch.materialType), layer, camera, 0, batch.matProperties);
        }
    }
}
