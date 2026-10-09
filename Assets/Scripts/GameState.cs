using UnityEngine;

public enum GamePhase { Setup, Playing }

public static class GameState
{
	public const int PlayerCount = 2;

	// Index of the player who claims first during Setup (0 = Player 1).
	public const int SetupFirstPlayer = 0;

	// Index of the player who acts first once Playing starts (1 = Player 2).
	public const int PlayFirstPlayer = 1;

	public static GamePhase Phase { get; private set; } = GamePhase.Setup;
	public static int CurrentPlayer { get; private set; } = SetupFirstPlayer;
	public static int Turn { get; private set; } = 1;

	static int claimsMade;

	/// <summary>Resources per player, indexed by player.</summary>
	public static Stockpile[] Stockpiles { get; private set; } = NewStockpiles();

	static Stockpile[] NewStockpiles()
	{
		var result = new Stockpile[PlayerCount];
		for (int i = 0; i < result.Length; i++)
		{
			result[i] = new Stockpile();
		}
		return result;
	}

	[RuntimeInitializeOnLoadMethod(
		RuntimeInitializeLoadType.SubsystemRegistration)]
	static void ResetState()
	{
		Phase = GamePhase.Setup;
		CurrentPlayer = SetupFirstPlayer;
		Turn = 1;
		claimsMade = 0;
		Stockpiles = NewStockpiles();
	}

	static int NextPlayer() => (CurrentPlayer + 1) % PlayerCount;

	/// <summary>
	/// Called once per player during Setup, right after they claim a city.
	/// Passes the turn to the next player, or starts Playing after the last claim.
	/// </summary>
	public static void CompleteClaim()
	{
		if (Phase != GamePhase.Setup)
		{
			return;
		}
		claimsMade++;
		if (claimsMade >= PlayerCount)
		{
			Phase = GamePhase.Playing;
			CurrentPlayer = PlayFirstPlayer;
			Turn = 1;
			foreach (Stockpile stockpile in Stockpiles)
			{
				// Temporary starting resources for testing.
				stockpile.Add(ResourceType.Stone, 5);
				stockpile.Add(ResourceType.Wood, 5);
				stockpile.Add(ResourceType.Grain, 5);
			}
			Debug.Log($"Setup complete. Turn {Turn}: Player {CurrentPlayer + 1}");
		}
		else
		{
			CurrentPlayer = NextPlayer();
			Debug.Log($"Setup: Player {CurrentPlayer + 1} to claim");
		}
	}

	/// <summary>
	/// Ends the turn only if it is the Playing phase and the requesting
	/// player is the one whose turn it is. Returns whether it was accepted.
	/// </summary>
	public static bool EndTurn(int requestingPlayer)
	{
		if (Phase != GamePhase.Playing || requestingPlayer != CurrentPlayer)
		{
			return false;
		}
		CurrentPlayer = NextPlayer();
		if (CurrentPlayer == PlayFirstPlayer)
		{
			Turn++;
		}
		Debug.Log($"Turn {Turn}: Player {CurrentPlayer + 1} " +
			$"[{Stockpiles[CurrentPlayer]}]");
		return true;
	}
}
