// Reusable kanim renderer shared between BuildingImageSnapshotter (export path)
// and BuildingPoseInspectorScreen (preview path). Init() creates a camera + RenderTexture
// sized to the building footprint. Render() draws the current frame without ReadPixels,
// so the inspector can keep the RT alive across pose changes. ReadPixels() materialises
// it to a Texture2D for the export caller. Cleanup() releases the camera and RT.

using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace OniExtract2024.building
{
    public class BuildingKanimRenderer
    {
        public const float PixelsPerCell = 200f;
        public const float PaddingPx = 400f;
        public const int DrawLayer = 30;

        private Camera cam;
        private RenderTexture rt;

        public RenderTexture Texture => rt;
        public int TexW { get; private set; }
        public int TexH { get; private set; }

        public void Init(int widthInCells, int heightInCells, Vector3 buildingPos)
        {
            int texW = Mathf.CeilToInt(widthInCells * PixelsPerCell + 2 * PaddingPx);
            int texH = Mathf.CeilToInt(heightInCells * PixelsPerCell + 2 * PaddingPx);
            TexW = texW;
            TexH = texH;

            rt = new RenderTexture(texW, texH, 24);
            rt.Create();

            var reference = CameraController.Instance.baseCamera;
            cam = CameraController.CloneCamera(reference, "OniExtract_BuildingCam");
            cam.transform.parent = reference.transform.parent;
            cam.targetTexture = rt;
            cam.enabled = false;
            cam.backgroundColor = Color.clear;
            cam.cullingMask = 1 << DrawLayer;

            var pos = buildingPos;
            pos.z = -100f;
            pos.y += heightInCells / 2f;
            if (widthInCells % 2 == 0)
                pos.x += 0.5f;
            cam.transform.position = pos;
            cam.orthographicSize = texH / (2f * PixelsPerCell);
            cam.aspect = (float)texW / texH;
        }

        // Draws the current KBAC state into the RT. Does not read pixels back.
        // The caller is responsible for disabling CameraController.Instance.baseCamera
        // before calling and re-enabling it after.
        public void Render(IEnumerable<KBatchedAnimController> kbacs)
        {
            if (cam == null || rt == null) return;

            KAnimBatchManager.Instance().UpdateActiveArea(new Vector2I(-9999, -9999), new Vector2I(9999, 9999));
            KAnimBatchManager.Instance().UpdateDirty(Time.frameCount);

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = prev;

            cam.enabled = true;
            foreach (var k in kbacs.OrderBy(k => k.transform.position.z))
                RenderBatch(GetBatch(k), cam, DrawLayer);
            cam.Render();
            cam.enabled = false;
        }

        // Reads the RT into a new Texture2D. Caller must Destroy() the result.
        // Does NOT release the RT — call Cleanup() for that.
        public Texture2D ReadPixels()
        {
            if (rt == null) return null;
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            return tex;
        }

        // Releases the camera and RT. Safe to call more than once.
        public void Cleanup()
        {
            if (cam != null)
            {
                Object.Destroy(cam.gameObject);
                cam = null;
            }
            if (rt != null)
            {
                rt.Release();
                Object.Destroy(rt);
                rt = null;
            }
        }

        // --- reflection shims for non-publicised Assembly-CSharp fields ---

        private static KAnimBatch GetBatch(KBatchedAnimController kbac) =>
            Traverse.Create(kbac).Field("batch").GetValue<KAnimBatch>();

        private static List<BatchSet> GetActiveBatchSets() =>
            Traverse.Create(KAnimBatchManager.Instance()).Field("activeBatchSets").GetValue<List<BatchSet>>();

        private static void RenderBatch(KAnimBatch batch, Camera camera, int layerOverride)
        {
            if (batch == null || batch.size == 0 || !batch.active ||
                batch.materialType == KAnimBatchGroup.MaterialType.UI)
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
                    if (owningSet != null) break;
                }
            }
            if (owningSet == null) return;

            float zNudge = 0.01f / (1 + batch.id % 256);
            Vector3 drawPos = Vector3.zero;
            drawPos.z = batch.position.z + zNudge;
            int layer = layerOverride >= 0 ? layerOverride : batch.layer;

            Graphics.DrawMesh(owningSet.group.mesh, drawPos, Quaternion.identity,
                owningSet.group.GetMaterial(batch.materialType), layer, camera, 0, batch.matProperties);
        }
    }
}
