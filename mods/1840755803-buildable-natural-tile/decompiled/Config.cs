using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;

namespace BuildableNaturalTile;

public class Config
{
	private static readonly string path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json");

	public float BuildMass { get; set; }

	public float BlockMass { get; set; }

	public float BuildSpeed { get; set; }

	public void Save()
	{
		string contents = JsonConvert.SerializeObject((object)this, (Formatting)1);
		File.WriteAllText(path, contents);
	}

	public static Config Load()
	{
		string text = null;
		Config config = null;
		bool flag = false;
		Dictionary<string, int> dictionary = new Dictionary<string, int>
		{
			{ "BuildMass", 50 },
			{ "BlockMass", 50 },
			{ "BuildSpeed", 3 }
		};
		if (!File.Exists(path))
		{
			flag = true;
			text = "{\n\t\t\t\t\tBuildMass: 50,\n\t\t\t\t\tBlockMass: 50,\n\t\t\t\t\tBuildSpeed: 3\n\t\t\t\t\t}";
		}
		else
		{
			text = File.ReadAllText(path);
		}
		config = JsonConvert.DeserializeObject<Config>(text);
		if (config.BuildMass == 0f)
		{
			config.BuildMass = dictionary["BuildMass"];
			flag = true;
		}
		if (config.BlockMass == 0f)
		{
			config.BlockMass = dictionary["BlockMass"];
			flag = true;
		}
		if (config.BuildSpeed == 0f)
		{
			config.BuildSpeed = dictionary["BuildSpeed"];
			flag = true;
		}
		if (flag)
		{
			config.Save();
		}
		return config;
	}
}
