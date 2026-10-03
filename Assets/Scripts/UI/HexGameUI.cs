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

	HexCell currentCell;

	HexUnit selectedUnit;

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
		if (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
		{
			selectedUnit = null;
			grid.ClearPath();
			GameState.EndTurn();
		}
		if (!EventSystem.current.IsPointerOverGameObject())
		{
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

	void DoSelection()
	{
		grid.ClearPath();
		UpdateCurrentCell();
		if (currentCell)
		{
			HexUnit unit = currentCell.Unit;
			selectedUnit =
				unit && unit.Owner == GameState.CurrentPlayer &&
				unit.CanMoveThisTurn ? unit : null;
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
