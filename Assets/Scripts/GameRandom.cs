public static class GameRandom
{
	static System.Random random = new System.Random();

	public static int Seed { get; private set; }

	public static void Init(int seed)
	{
		Seed = seed;
		random = new System.Random(seed);
	}

	/// <summary>Min inclusive, max exclusive, like UnityEngine.Random.Range.</summary>
	public static int Range(int min, int max) => random.Next(min, max);

	/// <summary>True with the given probability (0 to 1).</summary>
	public static bool Chance(float probability) =>
		random.NextDouble() < probability;
}