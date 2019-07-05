using PathFinding;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class GridBuilder : MonoBehaviour
{
	public Vector2Int gridSize;
	public float gridHeight = 10f;
	public float nodeRadius = 0.5f;
	[Range(0, 90)]
	public float maxWalkableAngle = 30;
	public LayerMask walkable = -1;

	public PathfindingGrid grid;

	public Transform tracked;
	public Transform target;

	private PathFindingRequest currentRequest;

	private Path path;
	private Stopwatch stopWatch = new Stopwatch();

	// Start is called before the first frame update
	void Start () {
		grid = BuildGrid();
		PathFindiningSystem.instance.UpdateGrid(grid);
	}


	private void Update () {
		if(tracked == null || target == null) {
			return;
		}

		if (currentRequest != null) {
			if (currentRequest.IsDone) {
				path = currentRequest.GetResult();
				currentRequest = null;
				print($"Took: {stopWatch.ElapsedMilliseconds}ms");
				stopWatch.Stop();
			}
		} else /*if(Input.GetKeyDown(KeyCode.Space))*/{
			stopWatch.Restart();
			currentRequest = new PathFindingRequest(tracked.position, target.position);

			currentRequest.Queue();
		}
	}

	private PathfindingGrid BuildGrid () {
		float nodeDiam = nodeRadius * 2f;
		int width = Mathf.CeilToInt(gridSize.x / nodeDiam);
		int height = Mathf.CeilToInt(gridSize.y / nodeDiam);

		PathfindingGrid grid = new PathfindingGrid();
		grid.width = width;
		grid.height = height;
		grid.grid = new Unity.Collections.NativeArray<Node>(width * height, Unity.Collections.Allocator.Persistent);
		grid.nodeSize = nodeDiam;

		for (int x = 0; x < width; x++) {
			for (int y = 0; y < height; y++) {

				var rayOrigin = grid.GetNodePosition(x, y);
				rayOrigin.y = gridHeight;
				var rayDir = Vector3.down;
				var ray = new Ray(rayOrigin, rayDir);

				if (Physics.Raycast(ray, out RaycastHit hit, gridHeight)) {
					bool obstructed = CheckCube(hit.point, nodeRadius);
					Node node = new Node() {
						yPosition = hit.point.y,
						walkable = !obstructed && (walkable == (walkable | (1 << hit.collider.gameObject.layer)))
					};

					grid.grid[y * width + x] = node;
				} else {
					Node node = new Node() {
						yPosition = ray.GetPoint(gridHeight).y,
						walkable = false
					};

					grid.grid[y * width + x] = node;
				}
			}
		}

		return grid;
	}

	private void OnDrawGizmosSelected () {
		Gizmos.DrawWireCube(Vector3.zero, new Vector3(gridSize.x, gridHeight, gridSize.y));
	}

	private void OnDrawGizmos () {
		if (Application.isPlaying && grid.width != 0 && grid.height != 0) {

			/*
			for (int x = 0; x < grid.width; x++) {
				for (int y = 0; y < grid.height; y++) {
					Node node = grid.GetNode(x, y);

					Gizmos.color = node.walkable ? Color.green : Color.red;

					if (tracked != null) {
						var idx = grid.GetNodeIndex(tracked.position);
						if (idx.x == x && idx.y == y) {
							Gizmos.color = Color.blue;
						}
					}


					Gizmos.DrawWireCube(grid.GetNodePosition(x, y), new Vector3(grid.nodeSize, 0.01f, grid.nodeSize));
				}
			}*/

			if (path != null && !path.failed) {

				Gizmos.color = Color.black;

				foreach(var node in path.nodes) {
					Gizmos.DrawCube(node, new Vector3(grid.nodeSize, 0.01f, grid.nodeSize));
				}
			}
		}
	}

	private bool CheckCube (Vector3 pos, float radius) {
		return Physics.CheckBox(pos + (Vector3.up * radius), Vector3.one * radius, Quaternion.identity, ~walkable, QueryTriggerInteraction.Ignore);
	}
}
