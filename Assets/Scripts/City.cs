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
			transform.localPosition = value.Position;
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
	}

	public void ValidateLocation() =>
		transform.localPosition = Grid.GetCell(locationCellIndex).Position;
		
	public void Die()
	{
		HexCell location = Grid.GetCell(locationCellIndex);
		Grid.DecreaseVisibility(location, VisionRange);
		location.City = null;
		Destroy(gameObject);
	}

	void Awake() => Owner = owner;
}