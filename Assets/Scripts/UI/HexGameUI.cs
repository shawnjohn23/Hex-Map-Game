using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Component that manages the game UI.
/// </summary>
public class HexGameUI : MonoBehaviour
{
	[SerializeField]
	HexGrid grid;

	[SerializeField]
	GameController gameController;

	HexCell currentCell;

	HexUnit selectedUnit;

	string hoverText;

	// A forest the player clicked, waiting for a number and Space.
	HexCell assignCell;
	City assignCity;
	int assignCount;

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
		if (Keyboard.current != null &&
			Keyboard.current.enterKey.wasPressedThisFrame)
		{
			// Only accepted during Playing, and only for the current player.
			int endingPlayer = GameState.CurrentPlayer;
			if (GameState.EndTurn(endingPlayer))
			{
				gameController.RunUpkeep(endingPlayer);
				CancelAssignment();
				selectedUnit = null;
				grid.ClearPath();
			}
		}
		if (!EventSystem.current.IsPointerOverGameObject())
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
		UpdateAssignment();
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
				$"(weak {city.WeakPop}, idle {city.Idle})\n" +
				$"Storage: {city.Storage}";
		}
	}

	// Digits 1-9 pick how many pops, Space confirms, Esc cancels.
	// (Arrow keys, WASD, Q and E are taken by the camera.)
	void UpdateAssignment()
	{
		if (!assignCell || Keyboard.current == null)
		{
			return;
		}
		if (!assignCity)
		{
			CancelAssignment();
			return;
		}
		Keyboard keyboard = Keyboard.current;
		for (int i = 1; i <= 9; i++)
		{
			if (keyboard[Key.Digit1 + (i - 1)].wasPressedThisFrame)
			{
				assignCount = Mathf.Min(i, assignCity.Idle);
			}
		}
		if (keyboard.spaceKey.wasPressedThisFrame)
		{
			gameController.TryAssign(assignCell, assignCity, assignCount);
			CancelAssignment();
			return;
		}
		if (keyboard.escapeKey.wasPressedThisFrame)
		{
			CancelAssignment();
			return;
		}
		hoverText = $"Assign {assignCount} of {assignCity.Idle} idle pops\n" +
			"1-9 number, Space confirm, Esc cancel";
	}

	void CancelAssignment()
	{
		assignCell = default;
		assignCity = null;
	}

	// Placeholder IMGUI tooltip, no scene setup needed.
	void OnGUI()
	{
		if (hoverText == null || Mouse.current == null)
		{
			return;
		}
		Vector2 m = Mouse.current.position.ReadValue();
		GUI.Box(new Rect(m.x + 16, Screen.height - m.y + 16, 300, 48), hoverText);
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
		CancelAssignment();
		if (currentCell)
		{
			HexUnit unit = currentCell.Unit;
			selectedUnit =
				unit && unit.Owner == GameState.CurrentPlayer &&
				unit.CanMoveThisTurn ? unit : null;
			if (!selectedUnit && gameController.IsWorkableForest(currentCell))
			{
				assignCity = gameController.ClosestCityWithIdle(
					currentCell, GameState.CurrentPlayer);
				if (assignCity)
				{
					assignCell = currentCell;
					assignCount = 1;
				}
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
