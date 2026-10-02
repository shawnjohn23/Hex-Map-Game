using System.Collections.Generic;

/// <summary>
/// An AI controller for one faction. Given the current game state, it returns
/// the single action it wants to take. Returning an EndTurnAction (or null)
/// signals it is done for this turn.
///
/// The human player does not implement this — the UI emits the same
/// IGameAction types directly. Same interface, two drivers.
/// </summary>
public interface IAgent
{
	IGameAction ChooseAction(GameState state);
}

/// <summary>
/// The simplest possible agent: pick a uniformly random legal action. Its only
/// job is to prove the loop closes — two of these can play a full game to a win
/// condition. It is the baseline every smarter (heuristic, then learned) agent
/// is measured against.
/// </summary>
public class RandomAgent : IAgent
{
	readonly System.Random rng;

	public RandomAgent(int seed) => rng = new System.Random(seed);

	public IGameAction ChooseAction(GameState state)
	{
		List<IGameAction> legal = state.EnumerateLegalActions();
		if (legal.Count == 0)
		{
			return new EndTurnAction();
		}

		// Bias slightly against ending the turn so the game actually develops:
		// prefer a non-EndTurn action if one exists, most of the time.
		var nonEnd = legal.FindAll(a => a is not EndTurnAction);
		if (nonEnd.Count > 0 && rng.NextDouble() < 0.9)
		{
			return nonEnd[rng.Next(nonEnd.Count)];
		}
		return legal[rng.Next(legal.Count)];
	}
}
