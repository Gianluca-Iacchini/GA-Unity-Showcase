using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MazeGenerator : MonoBehaviour
{
    [SerializeField]
    private GameObject _mazeCell;

    [SerializeField]
    public int _mazeWidth = 10;

    [SerializeField]
    public int _mazeHeight = 10;

    [SerializeField]
    public float _cellSize = 1.0f;

    private MazeCell[,] _mazeGrid;

    public MazeCell LastMazeCell { get; private set; } = null;

    public MazeCell[,] GenerateMaze()
    {
        _mazeGrid = new MazeCell[_mazeWidth, _mazeHeight];

        for (int x = 0; x < _mazeWidth; x++)
        {
            for (int z = 0; z < _mazeHeight; z++)
            {
                var cell = Instantiate(_mazeCell, this.transform.position, Quaternion.identity, this.transform);
                cell.transform.localScale = new Vector3(_cellSize, _cellSize, _cellSize);
                cell.transform.localPosition = new Vector3(x * _cellSize + _cellSize/2f, +_cellSize/2f, z * _cellSize + _cellSize/2f);
                _mazeGrid[x, z] = cell.GetComponent<MazeCell>();
            }
        }

        generateMaze(null, _mazeGrid[0, 0]);

        return _mazeGrid;
    }

    private void generateMaze(MazeCell previousCell, MazeCell currentCell)
    {
        currentCell.Visit();
        ClearWalls(previousCell, currentCell);

        MazeCell nextCell;

        do
        {
            nextCell = GetNextUnvisitedCell(currentCell);

            if (nextCell != null)
            {
                generateMaze(currentCell, nextCell);
                if (LastMazeCell == null)
                    LastMazeCell = nextCell;
            }
        } while (nextCell != null);

    }

    private void ClearWalls(MazeCell previousCell, MazeCell currentCell)
    {
        if (previousCell == null) return;

        if (previousCell.transform.position.x < currentCell.transform.position.x)
        {
            previousCell.ClearRightWall();
            currentCell.ClearLeftWall();
            return;
        }

        if (previousCell.transform.position.x > currentCell.transform.position.x)
        {
            previousCell.ClearLeftWall();
            currentCell.ClearRightWall();
            return;
        }

        if (previousCell.transform.position.z < currentCell.transform.position.z)
        {
            previousCell.ClearFrontWall();
            currentCell.ClearBackWall();
            return;
        }

        if (previousCell.transform.position.z > currentCell.transform.position.z)
        {
            previousCell.ClearBackWall();
            currentCell.ClearFrontWall();
            return;
        }
    }

    private MazeCell GetNextUnvisitedCell(MazeCell currentCell)
    {
        var unvisitedCells = GetUnvisitedCell(currentCell);
        return unvisitedCells.OrderBy(_ => Random.Range(1,10)).FirstOrDefault();
    }

    private IEnumerable<MazeCell> GetUnvisitedCell(MazeCell currentCell)
    {
        int x = Mathf.FloorToInt(currentCell.transform.position.x / _cellSize);
        int z = Mathf.FloorToInt(currentCell.transform.position.z / _cellSize);

        if (x + 1 < _mazeWidth)
        {
            var cellToRight = _mazeGrid[x + 1, z];
            if (!cellToRight.isVisited)
                yield return cellToRight;
        }

        if (x - 1 >= 0)
        {
            var cellToLeft = _mazeGrid[x - 1, z];
            if (!cellToLeft.isVisited)
                yield return cellToLeft;
        }

        if (z + 1 < _mazeHeight)
        {
            var cellToFront = _mazeGrid[x, z + 1];
            if (!cellToFront.isVisited)
                yield return cellToFront;
        }

        if (z - 1 >= 0)
        {
            var cellToBack = _mazeGrid[x, z - 1];
            if (!cellToBack.isVisited)
                yield return cellToBack;
        }
    }

    public Vector2Int Vector3ToMazeCoord(Vector3 coord)
    {
        int x = Mathf.FloorToInt(coord.x / _cellSize);
        int z = Mathf.FloorToInt(coord.z / _cellSize);

        return new Vector2Int(x, z);
    }

    public MazeCell GetCell(Vector2Int cell)
    {
        if (cell.x < 0 || cell.x >= _mazeWidth || cell.y < 0 || cell.y >= _mazeHeight)
            return null;

        return _mazeGrid[cell.x, cell.y];
    }

    public MazeCell GetCellFromVector3(Vector3 coords)
    {
        var cell = Vector3ToMazeCoord(coords);
        return GetCell(cell);
    }

    public Vector3 MazeCoordToVector3(Vector2Int cell)
    {
        return new Vector3(cell.x * _cellSize + _cellSize / 2f, _cellSize / 2f, cell.y * _cellSize + _cellSize / 2f);
    }
}
