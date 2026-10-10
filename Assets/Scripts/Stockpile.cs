using System;
using System.Collections.Generic;
using System.Text;

// Add new resources at the END of this list (existing values keep their index).
public enum ResourceType { Stone, Wood, Grain, Gold, Meat }

/// <summary>
/// One player's resources: a plain int per ResourceType, no scene objects.
/// </summary>
public class Stockpile
{
	public static readonly int TypeCount =
		Enum.GetValues(typeof(ResourceType)).Length;

	readonly int[] amounts = new int[TypeCount];

	/// <summary>Read-only view of all amounts, indexed by ResourceType.</summary>
	public IReadOnlyList<int> Amounts => amounts;

	public int Get(ResourceType type) => amounts[(int)type];

	public void Add(ResourceType type, int amount) =>
		amounts[(int)type] += amount;

	public bool CanAfford(ResourceType type, int amount) =>
		amounts[(int)type] >= amount;

	/// <summary>Spend if affordable. Returns whether it succeeded.</summary>
	public bool TrySpend(ResourceType type, int amount)
	{
		if (!CanAfford(type, amount))
		{
			return false;
		}
		amounts[(int)type] -= amount;
		return true;
	}

	public override string ToString()
	{
		var sb = new StringBuilder();
		for (int i = 0; i < amounts.Length; i++)
		{
			if (i > 0)
			{
				sb.Append(", ");
			}
			sb.Append((ResourceType)i).Append(' ').Append(amounts[i]);
		}
		return sb.ToString();
	}
}
