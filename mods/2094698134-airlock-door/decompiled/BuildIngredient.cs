using System;
using PeterHan.PLib.Core;
using TUNING;

namespace PeterHan.PLib.Buildings;

internal class BuildIngredient
{
	public string Material { get; }

	public float Quantity { get; }

	public BuildIngredient(string name, float quantity)
	{
		if (string.IsNullOrEmpty(name))
		{
			throw new ArgumentNullException("name");
		}
		if (quantity.IsNaNOrInfinity() || quantity <= 0f)
		{
			throw new ArgumentException("quantity");
		}
		Material = name;
		Quantity = quantity;
	}

	public BuildIngredient(string[] material, int tier)
		: this(material[0], tier)
	{
	}

	public BuildIngredient(string material, int tier)
	{
		if (string.IsNullOrEmpty(material))
		{
			throw new ArgumentNullException("material");
		}
		Material = material;
		Quantity = tier switch
		{
			-1 => CONSTRUCTION_MASS_KG.TIER_TINY[0], 
			0 => CONSTRUCTION_MASS_KG.TIER0[0], 
			1 => CONSTRUCTION_MASS_KG.TIER1[0], 
			2 => CONSTRUCTION_MASS_KG.TIER2[0], 
			3 => CONSTRUCTION_MASS_KG.TIER3[0], 
			4 => CONSTRUCTION_MASS_KG.TIER4[0], 
			5 => CONSTRUCTION_MASS_KG.TIER5[0], 
			6 => CONSTRUCTION_MASS_KG.TIER6[0], 
			7 => CONSTRUCTION_MASS_KG.TIER7[0], 
			_ => throw new ArgumentException("tier must be between -1 and 7 inclusive"), 
		};
	}

	public override bool Equals(object obj)
	{
		if (obj is BuildIngredient buildIngredient && buildIngredient.Material == Material)
		{
			return buildIngredient.Quantity == Quantity;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return Material.GetHashCode();
	}

	public override string ToString()
	{
		return "Material[Tag={0},Quantity={1:F0}]".F(Material, Quantity);
	}
}
