using Newtonsoft.Json;
using PeterHan.PLib.Options;

namespace Drains;

[JsonObject(/*Could not decode attribute arguments.*/)]
[ModInfo("https://github.com/skairunner/sky-oni-mods", null, false)]
[RestartRequired]
public class DrainOptions : SingletonOptions<DrainOptions>
{
	[JsonProperty]
	[Option("Solid Drains", "Drains will be solid and absorb water on the cell above them.", null)]
	public bool UseSolidDrain { get; set; }

	[Option("Flow Rate", "Determines the rate of liquid intake measured in kg/s.", null, Format = "F1")]
	[Limit(0.10000000149011612, 1.0)]
	[JsonProperty]
	public float FlowRate { get; set; }

	public DrainOptions()
	{
		UseSolidDrain = false;
		FlowRate = 0.1f;
	}
}
