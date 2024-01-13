using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Class to keep track of the maze cell's data
/// </summary>
public class MazeCell : MonoBehaviour
{
    [SerializeField]
    private GameObject _leftWall;

    [SerializeField]
    private GameObject _rightWall;

    [SerializeField]
    private GameObject _backWall;

    [SerializeField]
    private GameObject _frontWall;

    [SerializeField]
    private GameObject _unvisited;

    public float CellValue = 0.0f;
    public bool isVisited { get; private set; }

    public HashSet<Vector2Int> VisibleCells { get; private set; } = new HashSet<Vector2Int>();

    public void Visit()
    {
        isVisited = true;
        _unvisited.SetActive(false);
    }

    public void ClearLeftWall()
    {
        _leftWall.SetActive(false);
        VisibleCells.Add(new Vector2Int(-1, 0));
    }

    public void ClearRightWall()
    {
        _rightWall.SetActive(false);
        VisibleCells.Add(new Vector2Int(1, 0));
    }

    public void ClearBackWall()
    {
        _backWall.SetActive(false);
        VisibleCells.Add(new Vector2Int(0, -1));
    }

    public void ClearFrontWall()
    {
        _frontWall.SetActive(false);
        VisibleCells.Add(new Vector2Int(0, 1));
    }

    
}
