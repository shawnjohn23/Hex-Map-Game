using UnityEngine;

public enum CityState { Candidate, Claimed }

public class City : MonoBehaviour
{
	public HexGrid Grid { get; set; }

	public CityState State { get; private set; } = CityState.Candidate;

	public int VisionRange => 5;

	int locationCellIndex = -1;
	int owner = PlayerColors.Neutral;

	public HexCell Location
	{
		get => Grid.GetCell(locationCellIndex);
		set
		{
			if (locationCellIndex >= 0)
			{
				HexCell location = Grid.GetCell(locationCellIndex);
				Grid.DecreaseVisibility(location, VisionRange);
				location.City = null;
			}
			locationCellIndex = value.Index;
			value.City = this;
			Grid.IncreaseVisibility(value, VisionRange);
			transform.localPosition = MarkerPosition(value);
			Grid.MakeChildOfColumn(transform, value.Coordinates.ColumnIndex);
		}
	}

	public int Owner
	{
		get => owner;
		set
		{
			owner = value;
			foreach (Renderer r in GetComponentsInChildren<Renderer>())
			{
				r.material.color = PlayerColors.Colors[value];
			}
		}
	}

	public void Claim(int player)
	{
		Owner = player;
		State = CityState.Claimed;

		// The map's urban feature draws the houses; the owner tints them.
		HexCell cell = Location;
		cell.SetOwner(player);
		cell.SetUrbanLevel(1);
	}

	public void ValidateLocation() =>
		transform.localPosition = MarkerPosition(Grid.GetCell(locationCellIndex));

	// On river cells, move the marker off the river toward the first
	// edge the river does not cross, so it sits beside the water.
	static Vector3 MarkerPosition(HexCell cell)
	{
		Vector3 position = cell.Position;
		HexFlags flags = cell.Flags;
		if (flags.HasAny(HexFlags.River))
		{
			for (HexDirection d = HexDirection.NE; d <= HexDirection.NW; d++)
			{
				if (!flags.HasRiver(d))
				{
					position += (HexMetrics.GetFirstSolidCorner(d) +
						HexMetrics.GetSecondSolidCorner(d)) * 0.5f;
					break;
				}
			}
		}
		return position;
	}

	public void Die()
	{
		HexCell location = Grid.GetCell(locationCellIndex);
		Grid.DecreaseVisibility(location, VisionRange);
		location.City = null;
		if (State == CityState.Claimed)
		{
			// Undo what Claim changed on the cell.
			location.SetUrbanLevel(0);
			location.SetOwner(PlayerColors.Neutral);
		}
		Destroy(gameObject);
	}

	void Awake() => Owner = owner;
}
