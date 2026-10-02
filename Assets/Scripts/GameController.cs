using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Unity-side glue that owns the GameState and runs the turn loop. This is
/// intentionally the ONLY MonoBehaviour in the faction kit — all rules and
/// state live in plain classes (FactionState, GameState, the actions) so they
/// stay testable and, eventually, simulatable without rendering.
///
/// Attach to a GameObject in the scene and assign the HexGrid in the inspector.
/// </summary>
public class GameController : MonoBehaviour
{
	[SerializeField] HexGrid grid;

	[Header("Win condition")]
	[Tooltip("A faction wins when it owns this fraction of all land cells.")]
	[Range(0.1f, 1f)]
	[SerializeField] float landFractionToWin = 0.6f;

	[Header("Self-play")]
	[Tooltip("Run two AI agents against each other automatically.")]
	[SerializeField] bool autoSelfPlay = true;
	[SerializeField] float secondsPerAction = 0.1f;

	public GameState State { get; private set; }

	FactionState factions;
	readonly Dictionary<int, IAgent> agents = new(); // ownerId -> agent
	bool gameOver;

	void Start()
	{
		factions = new FactionState();

		// Two example factions. Owner ids are 1-based; 0 stays neutral.
		factions.SetFactions(new[]
		{
			new Faction(1, "Red",  Color.red),
			new Faction(2, "Blue", Color.blue)
		});

		// Wire the state layer into the grid so it serializes with the map and
		// is resized whenever a map is (re)created.
		grid.Factions = factions;
		factions.OnMapCreated(grid.CellData.Length);

		State = new GameState(grid, factions);

		SeedStartingTerritory();

		if (autoSelfPlay)
		{
			agents[1] = new RandomAgent(seed: 1);
			agents[2] = new RandomAgent(seed: 2);
			StartCoroutine(SelfPlayLoop());
		}
	}

	/// <summary>Give each faction one starting cell so claims have a seed to
	/// grow from. Placeholder until proper start-position logic exists.</summary>
	void SeedStartingTerritory()
	{
		int n = grid.CellData.Length;
		if (n == 0)
		{
			return;
		}
		// Crude: first land cell from each end of the array.
		int redSeed = FirstLandCell(0, 1);
		int blueSeed = FirstLandCell(n - 1, -1);
		if (redSeed >= 0)
		{
			factions.SetOwner(redSeed, 1);
		}
		if (blueSeed >= 0 && blueSeed != redSeed)
		{
			factions.SetOwner(blueSeed, 2);
		}
	}

	int FirstLandCell(int start, int step)
	{
		int n = grid.CellData.Length;
		for (int i = start; i >= 0 && i < n; i += step)
		{
			if (!grid.GetCell(i).Values.IsUnderwater)
			{
				return i;
			}
		}
		return -1;
	}

	IEnumerator SelfPlayLoop()
	{
		var wait = new WaitForSeconds(secondsPerAction);
		while (!gameOver)
		{
			int me = State.CurrentOwnerId;
			IAgent agent = agents.TryGetValue(me, out IAgent a) ? a : null;

			if (agent == null)
			{
				// No agent for this faction (e.g. a human) — skip its turn in
				// auto mode. A human turn would be driven by UI instead.
				new EndTurnAction().Apply(State);
				yield return wait;
				continue;
			}

			IGameAction action = agent.ChooseAction(State);
			if (action == null || action is EndTurnAction)
			{
				new EndTurnAction().Apply(State);
			}
			else if (action.IsLegal(State))
			{
				action.Apply(State);
			}

			CheckWinCondition();
			yield return wait;
		}
	}

	void CheckWinCondition()
	{
		int land = CountLandCells();
		if (land == 0)
		{
			return;
		}
		foreach (Faction f in factions.Factions)
		{
			float frac = factions.CountOwned(f.Id) / (float)land;
			if (frac >= landFractionToWin)
			{
				gameOver = true;
				Debug.Log($"{f.Name} wins on turn {factions.TurnNumber} " +
					$"with {frac:P0} of the land.");
				return;
			}
		}
	}

	int CountLandCells()
	{
		int n = grid.CellData.Length;
		int land = 0;
		for (int i = 0; i < n; i++)
		{
			if (!grid.GetCell(i).Values.IsUnderwater)
			{
				land++;
			}
		}
		return land;
	}
}
