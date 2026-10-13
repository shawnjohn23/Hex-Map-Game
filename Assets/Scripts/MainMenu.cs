using UnityEngine;

/// <summary>
/// Placeholder start screen: New Game / Map Editor / Quit.
/// Swap the drawing for real UI later; the calls it makes stay the same.
/// </summary>
public class MainMenu : MonoBehaviour
{
	enum MenuScreen { Main, NewGame, None }

	const float Width = 320f;
	const float Row = 28f;
	const float Pad = 8f;

	static readonly string[] SizeNames = { "Small", "Medium", "Large" };
	static readonly int[] SizeX = { 20, 40, 80 };
	static readonly int[] SizeZ = { 15, 30, 60 };

	[SerializeField]
	HexMapGenerator generator;

	[SerializeField]
	GameController gameController;

	[SerializeField]
	HexGameUI gameUI;
	
	[SerializeField]
	HexMapEditor editor;

	[SerializeField]
	float uiScale = 1.6f;

	MenuScreen screen = MenuScreen.Main;
	int size;
	string seedText = "";
	int lastSize, lastSeed;

	void Start() => ShowMenu();

	/// <summary>Back to the main menu, with editing tools and game input off.</summary>
	public void ShowMenu()
	{
		screen = MenuScreen.Main;
		editor.SetEditing(false);
		gameUI.enabled = false;
		HexMapCamera.Locked = true;
	}

	/// <summary>Same map, same seed, same start positions as last game.</summary>
	public void PlayAgain() => Begin(lastSize, lastSeed);

	void Begin(int sizeIndex, int seed)
	{
		lastSize = sizeIndex;
		lastSeed = seed;
		generator.GenerateMap(
			SizeX[sizeIndex], SizeZ[sizeIndex], false, false, seed);
		HexMapCamera.ValidatePosition();
		gameController.StartNewGame(seed);
		CloseMenu(false);
	}

	// Playing: editing tools hidden, game input on. Editing: the reverse.
	void CloseMenu(bool editing)
	{
		screen = MenuScreen.None;
		HexMapCamera.Locked = false;
		editor.SetEditing(editing);
	}

	int ParseSeed() =>
		int.TryParse(seedText, out int seed) && seed > 0 ?
			seed : Random.Range(1, int.MaxValue);

	void OnGUI()
	{
		if (screen == MenuScreen.None)
		{
			return;
		}
		GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
		float rows = screen == MenuScreen.Main ? 4 : 6;
		float height = Pad * 2 + Row * rows;
		var box = new Rect(
			(Screen.width / uiScale - Width) / 2,
			(Screen.height / uiScale - height) / 2, Width, height);
		GUI.Box(box, GUIContent.none);

		float x = box.x + Pad;
		float y = box.y + Pad;
		float w = Width - Pad * 2;
		float h = Row - 2;

		if (screen == MenuScreen.Main)
		{
			GUI.Label(new Rect(x, y, w, Row), "Island Game");
			y += Row;
			if (GUI.Button(new Rect(x, y, w, h), "New Game"))
			{
				screen = MenuScreen.NewGame;
			}
			y += Row;
			if (GUI.Button(new Rect(x, y, w, h), "Map Editor"))
			{
				CloseMenu(true);
			}
			y += Row;
			if (GUI.Button(new Rect(x, y, w, h), "Quit"))
			{
#if UNITY_EDITOR
				UnityEditor.EditorApplication.isPlaying = false;
#else
				Application.Quit();
#endif
			}
			return;
		}

		GUI.Label(new Rect(x, y, w, Row), "New Game");
		y += Row;
		GUI.Label(new Rect(x, y, w, Row), "Map size");
		y += Row;
		size = GUI.Toolbar(new Rect(x, y, w, h), size, SizeNames);
		y += Row;
		GUI.Label(new Rect(x, y, w, Row), "Seed (blank = random)");
		y += Row;
		seedText = GUI.TextField(new Rect(x, y, w, h), seedText, 10);
		y += Row;
		if (GUI.Button(new Rect(x, y, w / 2 - 4, h), "Back"))
		{
			screen = MenuScreen.Main;
		}
		if (GUI.Button(new Rect(x + w / 2 + 4, y, w / 2 - 4, h), "Start"))
		{
			Begin(size, ParseSeed());
		}
	}
}