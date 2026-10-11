using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Component that manages the game UI.
/// </summary>
public class HexGameUI : MonoBehaviour
{
	const float MenuWidth = 280f;
	const float RowHeight = 26f;
	const float Pad = 8f;

	[SerializeField]
	HexGrid grid;

	[SerializeField]
	GameController gameController;

	HexCell currentCell;

	HexUnit selectedUnit;

	string hoverText;

	// The forest whose action menu is open (default = no menu).
	HexCell menuCell;
	int sendCount = 1;
	Rect menuRect;

	// IMGUI is tiny on big screens; this scales all of it (tooltip, HUD, menu).
	[SerializeField]
	float uiScale = 1.6f;

	InputAction selectAction, commandAction, positionAction;

	void Awake()
	{
		selectAction = InputSystem.actions.FindAction("Interact");
		commandAction = InputSystem.actions.FindAction("Command");
		positionAction = InputSystem.actions.FindAction("Position");
	}

	/// <summary>
	/// Set whether map edit mode is active.
	/// </summary>
	/// <param name="toggle">Whether edit mode is enabled.</param>
	public void SetEditMode(bool toggle)
	{
		enabled = !toggle;
		grid.ShowUI(!toggle);
		grid.ClearPath();
		if (toggle)
		{
			Shader.EnableKeyword("_HEX_MAP_EDIT_MODE");
		}
		else
		{
			Shader.DisableKeyword("_HEX_MAP_EDIT_MODE");
		}
	}

	void Update()
	{
		hoverText = null;
		if (Keyboard.current != null)
		{
			if (Keyboard.current.enterKey.wasPressedThisFrame)
			{
				// Only accepted during Playing, and only for the current player.
				int endingPlayer = GameState.CurrentPlayer;
				if (GameState.EndTurn(endingPlayer))
				{
					gameController.RunUpkeep(endingPlayer);
					CloseMenu();
					selectedUnit = null;
					grid.ClearPath();
				}
			}
			if (menuCell && Keyboard.current.escapeKey.wasPressedThisFrame)
			{
				CloseMenu();
			}
		}
		if (!EventSystem.current.IsPointerOverGameObject() && !PointerOverMenu())
		{
			UpdateHover();
			if (selectAction.WasPerformedThisFrame())
			{
				DoSelection();
			}
			else if (selectedUnit)
			{
				if (commandAction.WasPerformedThisFrame())
				{
					DoMove();
				}
				else
				{
					DoPathfinding();
				}
			}
		}
	}

	void UpdateHover()
	{
		if (GameState.Phase != GamePhase.Playing)
		{
			return;
		}
		HexCell cell = grid.GetCell(
			Camera.main.ScreenPointToRay(positionAction.ReadValue<Vector2>()));
		if (!cell)
		{
			return;
		}
		City city = cell.City;
		if (city && city.State == CityState.Claimed &&
			city.Owner == GameState.CurrentPlayer)
		{
			hoverText = $"Pop {city.Population}/{city.PopulationCapacity} " +
				$"(weak {city.WeakPop}, idle {city.Idle}, " +
				$"walking home {city.ReturningPops})\n" +
				$"Storage: {city.Storage}";
		}
	}

	void CloseMenu() => menuCell = default;

	// IMGUI doesn't block Input System clicks, so check the menu ourselves.
	bool PointerOverMenu()
	{
		if (!menuCell || Mouse.current == null)
		{
			return false;
		}
		Vector2 m = Mouse.current.position.ReadValue() / uiScale;
		return menuRect.Contains(
			new Vector2(m.x, Screen.height / uiScale - m.y));
	}

	// Placeholder IMGUI, no scene setup needed.
	void OnGUI()
	{
		GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));
		if (GameState.Phase == GamePhase.Playing)
		{
			GUI.Box(new Rect(10, 10, 260, 52),
				$"Player {GameState.CurrentPlayer + 1}, turn {GameState.Turn}\n" +
				$"Free pops: {gameController.FreePops(GameState.CurrentPlayer)}");
			DrawMenu();
		}
		if (hoverText != null && Mouse.current != null)
		{
			Vector2 m = Mouse.current.position.ReadValue() / uiScale;
			GUI.Box(new Rect(m.x + 16, Screen.height / uiScale - m.y + 16,
				400, 60), hoverText);
		}
	}

	// Explicit rects (no GUILayout), so changing the party list in the
	// middle of a click can't cause layout errors.
	void DrawMenu()
	{
		if (!menuCell)
		{
			return;
		}
		int player = GameState.CurrentPlayer;
		if (!gameController.IsWorkable(menuCell, player))
		{
			DrawBuildMenu();
			return;
		}
		// Room left on the site (farm level, or 2 for a forest).
		int room = gameController.MaxWorkers(menuCell) -
			gameController.WorkersAt(menuCell);
		City sender = room > 0 ?
			gameController.ClosestCityWithIdle(menuCell, player) : null;
		List<WorkParty> parties = gameController.PartiesAt(menuCell, player);

		float height = Pad * 2 + RowHeight * (3 + parties.Count);
		menuRect = new Rect(10, 70, MenuWidth, height);
		GUI.Box(menuRect, GUIContent.none);

		float x = menuRect.x + Pad;
		float y = menuRect.y + Pad;
		float w = MenuWidth - Pad * 2;
		float h = RowHeight - 2;

		bool isFarm = menuCell.Values.PlantLevel <= 0 &&
			menuCell.Values.FarmLevel > 0;
		GUI.Label(new Rect(x, y, w, RowHeight),
			(isFarm ? $"Farm, level {menuCell.Values.FarmLevel}" :
			$"Forest, level {menuCell.Values.PlantLevel}") +
			$" ({gameController.WorkersAt(menuCell)}/" +
			$"{gameController.MaxWorkers(menuCell)} pops)");
		y += RowHeight;

		if (sender)
		{
			int most = Mathf.Min(sender.Idle, room);
			sendCount = Mathf.Clamp(sendCount, 1, most);
			if (GUI.Button(new Rect(x, y, 28, h), "-"))
			{
				sendCount = Mathf.Max(1, sendCount - 1);
			}
			GUI.Label(new Rect(x + 36, y, 24, RowHeight), sendCount.ToString());
			if (GUI.Button(new Rect(x + 60, y, 28, h), "+"))
			{
				sendCount = Mathf.Min(most, sendCount + 1);
			}
			if (GUI.Button(new Rect(x + 96, y, w - 96, h),
				$"Send {sendCount} (of {sender.Idle} idle)"))
			{
				gameController.TryAssign(menuCell, sender, sendCount);
			}
		}
		else
		{
			GUI.Label(new Rect(x, y, w, RowHeight),
				room > 0 ? "No idle pops available" : "Site is full");
		}
		y += RowHeight;

		foreach (WorkParty party in parties)
		{
			GUI.Label(new Rect(x, y, 110, RowHeight), $"{party.Working}/{party.Count} working");
			if (GUI.Button(new Rect(x + 110, y, 70, h), "Recall 1"))
			{
				gameController.TryUnassign(menuCell, party.Home, 1);
			}
			if (GUI.Button(new Rect(x + 184, y, w - 184, h), "All"))
			{
				gameController.TryUnassign(menuCell, party.Home, party.Count);
			}
			y += RowHeight;
		}

		if (GUI.Button(new Rect(x, y, w, h), "Close"))
		{
			CloseMenu();
		}
	}

	static readonly BuildKind[] BuildOptions = { BuildKind.Farm, BuildKind.House };

	// Menu for an empty, legal building cell: pick what to build there.
	void DrawBuildMenu()
	{
		int player = GameState.CurrentPlayer;
		City sender = gameController.ClosestCityWithIdle(menuCell, player);

		menuRect = new Rect(10, 70, MenuWidth,
			Pad * 2 + RowHeight * (2 + BuildOptions.Length));
		GUI.Box(menuRect, GUIContent.none);

		float x = menuRect.x + Pad;
		float y = menuRect.y + Pad;
		float w = MenuWidth - Pad * 2;
		float h = RowHeight - 2;

		GUI.Label(new Rect(x, y, w, RowHeight), sender ?
			$"Empty land (builder from {sender.Idle} idle)" :
			"Empty land: no idle pops to build");
		y += RowHeight;

		foreach (BuildKind kind in BuildOptions)
		{
			int cost = ConstructionSite.WoodCost(kind);
			GUI.enabled = sender &&
				sender.Storage.CanAfford(ResourceType.Wood, cost);
			if (GUI.Button(new Rect(x, y, w, h), $"Build {kind} ({cost} wood)"))
			{
				gameController.TryBuild(menuCell, kind, player);
				CloseMenu();
			}
			GUI.enabled = true;
			y += RowHeight;
		}

		if (GUI.Button(new Rect(x, y, w, h), "Close"))
		{
			CloseMenu();
		}
	}

	void DoSelection()
	{
		grid.ClearPath();
		UpdateCurrentCell();
		if (GameState.Phase == GamePhase.Setup)
		{
			selectedUnit = null;
			if (currentCell)
			{
				gameController.TryClaim(currentCell);
			}
			return;
		}
		CloseMenu();
		if (currentCell)
		{
			// Debug: why can't I build here? (delete once building works)
			Debug.Log($"Clicked {currentCell.Coordinates}: build blocker = " +
				$"{gameController.BuildBlocker(currentCell, GameState.CurrentPlayer) ?? "none"}");
			HexUnit unit = currentCell.Unit;
			selectedUnit =
				unit && unit.Owner == GameState.CurrentPlayer &&
				unit.CanMoveThisTurn ? unit : null;
			if (!selectedUnit &&
				(gameController.IsWorkable(currentCell, GameState.CurrentPlayer) ||
				gameController.IsBuildable(currentCell, GameState.CurrentPlayer)))
			{
				menuCell = currentCell;
				sendCount = 1;
			}
		}
	}

	void DoPathfinding()
	{
		if (UpdateCurrentCell())
		{
			if (currentCell && selectedUnit.IsValidDestination(currentCell))
			{
				grid.FindPath(selectedUnit.Location, currentCell, selectedUnit);
			}
			else
			{
				grid.ClearPath();
			}
		}
	}

	void DoMove()
	{
		if (grid.HasPath)
		{
			List<int> path = grid.GetPath();
			int reach = 0;
			for (int i = 1; i < path.Count; i++)
			{
				if (grid.SearchData[path[i]].distance > selectedUnit.MovementLeft)
				{
					break;
				}
				reach = i;
			}

			if (reach > 0)
			{
				Debug.Log($"Moving {reach} of {path.Count - 1} steps");
				int cost = grid.SearchData[path[reach]].distance;
				path.RemoveRange(reach + 1, path.Count - reach - 1);
				selectedUnit.SpendMovement(cost);
				selectedUnit.Travel(path);
			}
			else
			{
				ListPool<int>.Add(path);
			}
			grid.ClearPath();
			selectedUnit = null;
		}
	}

	bool UpdateCurrentCell()
	{
		HexCell cell = grid.GetCell(
			Camera.main.ScreenPointToRay(positionAction.ReadValue<Vector2>()));
		if (cell && cell != currentCell)
		{
			currentCell = cell;
			return true;
		}
		return false;
	}
}
