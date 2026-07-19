using Newtonsoft.Json;
using PeterHan.PLib.Options;

namespace High_Pressure_Applications.Components;

[JsonObject(/*Could not decode attribute arguments.*/)]
[RestartRequired]
public class HPA_ModSettings : SingletonOptions<HPA_ModSettings>
{
	[Option("High-Pressure Gas Pipe Capacity", "", null)]
	[Limit(2.0, 10.0)]
	[JsonProperty]
	public int HPGas { get; set; }

	[Option("High-Pressure Liquid Pipe Capacity", "", null)]
	[Limit(11.0, 100.0)]
	[JsonProperty]
	public int HPLiquid { get; set; }

	public HPA_ModSettings()
	{
		HPGas = 10;
		HPLiquid = 40;
	}
}
