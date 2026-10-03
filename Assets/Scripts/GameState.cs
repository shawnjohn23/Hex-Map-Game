using UnityEngine;

public static class GameState
{
	public const int PlayerCount = 2;

	public static int CurrentPlayer { get; private set; }
	public static int Turn { get; private set; } = 1;

	[RuntimeInitializeOnLoadMethod(
		RuntimeInitializeLoadType.SubsystemRegistration)]
	static void ResetState()
	{
		CurrentPlayer = 0;
		Turn = 1;
	}

	public static void EndTurn()
	{
		CurrentPlayer = (CurrentPlayer + 1) % PlayerCount;
		if (CurrentPlayer == 0)
		{
			Turn++;
		}
		Debug.Log($"Turn {Turn}: Player {CurrentPlayer + 1}");
	}
}