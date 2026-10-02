using UnityEngine;

/// <summary>
/// A player/faction in the game. Pure data — no Unity behaviour.
/// Owner id 0 is reserved for "neutral / unowned"; real factions are 1..N.
/// </summary>
[System.Serializable]
public class Faction
{
	/// <summary>Owner id used in the per-cell owner array. 1-based.</summary>
	public int Id;

	/// <summary>Display name, e.g. "Red".</summary>
	public string Name;

	/// <summary>Colour used to tint owned cells / units in the UI.</summary>
	public Color Color;

	/// <summary>True if a human controls this faction (vs. an AI agent).</summary>
	public bool IsHuman;

	public Faction(int id, string name, Color color, bool isHuman = false)
	{
		Id = id;
		Name = name;
		Color = color;
		IsHuman = isHuman;
	}
}
