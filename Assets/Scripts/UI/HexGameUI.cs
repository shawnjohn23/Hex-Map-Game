using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

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
			selectedUnit = currentCell.Unit;
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
			selectedUnit.Travel(grid.GetPath());
			grid.ClearPath();
		}
	}

	bool UpdateCurrentCell()
	{
		HexCell cell = grid.GetCell(
			Camera.main.ScreenPointToRay(positionAction.ReadValue<Vector2>()));
		if (cell)
		{
			currentCell = cell;
			return true;
		}
		return false;
	}
}
