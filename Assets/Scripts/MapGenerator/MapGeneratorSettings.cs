using UnityEngine;

/// <summary>
/// Settings for map generation. Shared between old and experimental generators.
/// </summary>
[System.Serializable]
public class MapGeneratorSettings
{
	public bool useFixedSeed;

	public int seed;

	[Range(0f, 0.5f)]
	public float jitterProbability = 0.25f;

	[Range(20, 200)]
	public int chunkSizeMin = 30;

	[Range(20, 200)]
	public int chunkSizeMax = 100;

	[Range(0f, 1f)]
	public float highRiseProbability = 0.25f;

	[Range(0f, 0.4f)]
	public float sinkProbability = 0.2f;

	[Range(5, 95)]
	public int landPercentage = 50;

	[Range(1, 5)]
	public int waterLevel = 3;

	[Range(-4, 0)]
	public int elevationMinimum = -2;

	[Range(6, 10)]
	public int elevationMaximum = 8;

	[Range(0, 10)]
	public int mapBorderX = 5;

	[Range(0, 10)]
	public int mapBorderZ = 5;

	[Range(0, 10)]
	public int regionBorder = 5;

	[Range(1, 4)]
	public int regionCount = 1;

	[Range(0, 100)]
	public int erosionPercentage = 50;

	[Range(0f, 1f)]
	public float startingMoisture = 0.1f;

	[Range(0f, 1f)]
	public float evaporationFactor = 0.5f;

	[Range(0f, 1f)]
	public float precipitationFactor = 0.25f;

	[Range(0f, 1f)]
	public float runoffFactor = 0.25f;

	[Range(0f, 1f)]
	public float seepageFactor = 0.25f;

	public HexDirection windDirection = HexDirection.NW;

	[Range(1f, 10f)]
	public float windStrength = 4f;

	[Range(0, 20)]
	public int riverPercentage = 10;

	[Range(0f, 1f)]
	public float extraLakeProbability = 0.25f;

	[Range(0f, 1f)]
	public float lowTemperature = 0f;

	[Range(0f, 1f)]
	public float highTemperature = 1f;

	public enum HemisphereMode
	{
		Both, North, South
	}

	public HemisphereMode hemisphere;

	[Range(0f, 1f)]
	public float temperatureJitter = 0.1f;
}
