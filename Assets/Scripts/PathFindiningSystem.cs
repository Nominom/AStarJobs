using PathFinding;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace PathFinding
{
	public class PathFindiningSystem : MonoBehaviour
	{
		public static PathFindiningSystem instance;
		private Queue<PathFindingRequest> requests = new Queue<PathFindingRequest>();
		private PathFindingRequest currentRequest;
		private JobHandle jobHandle;
		private ProcessPathJob job;
		private int framesProcessed = 0;

		private PathfindingGrid grid;

		[BurstCompile]
		private struct ProcessPathJob : IJob
		{
			public struct NodeCost : IEquatable<NodeCost>, IComparable<NodeCost>
			{
				public int2 idx;
				public int gCost;
				public int hCost;
				public int2 origin;

				public NodeCost (int2 i, int2 origin) {
					this.idx = i;
					this.origin = origin;
					this.gCost = 0;
					this.hCost = 0;
				}

				public int CompareTo (NodeCost other) {
					int compare = fCost().CompareTo(other.fCost());
					if (compare == 0) {
						compare = hCost.CompareTo(other.hCost);
					}
					return -compare;
				}

				public bool Equals (NodeCost other) {
					var b = (this.idx == other.idx);
					return math.all(b);
				}

				public int fCost () {
					return gCost + hCost;
				}

				public override int GetHashCode () {
					return idx.GetHashCode();
				}
			}

			public PathfindingGrid grid;
			public NativeList<int2> result;
			public NativeBinaryHeap<NodeCost> open;
			public NativeHashMap<int2, NodeCost> closed;
			public float3 srcPosition;
			public float3 dstPosition;

			public void Execute () {

				int2 startNode = grid.GetNodeIndex(srcPosition);
				int2 endNode = grid.GetNodeIndex(dstPosition);

				if (startNode.x == -1 || endNode.x == -1) {
					return;
				}


				open.Add(new NodeCost(startNode, startNode));

				int2 boundsMin = new int2(0, 0);
				int2 boundsMax = new int2 { x = grid.width, y = grid.height };

				NodeCost currentNode = new NodeCost(startNode, startNode);

				while (open.Count > 0) {
					currentNode = open.RemoveFirst();

					if (!closed.TryAdd(currentNode.idx, currentNode))
						break;

					if (math.all(currentNode.idx == endNode)) {
						break;
					}

					for (int xC = -1; xC <= 1; xC++) {
						for (int yC = -1; yC <= 1; yC++) {
							int2 newIdx = +currentNode.idx + new int2(xC, yC);

							if (math.all(newIdx >= boundsMin & newIdx < boundsMax)) {
								Node neighbor = grid.GetNode(newIdx.x, newIdx.y);

								NodeCost newCost = new NodeCost(newIdx, currentNode.idx);

								if (!neighbor.walkable || closed.TryGetValue(newIdx, out NodeCost _)) {
									continue;
								}

								int newGCost = currentNode.gCost + NodeDistance(currentNode.idx, newIdx);

								newCost.gCost = newGCost;
								newCost.hCost = NodeDistance(newIdx, endNode);



								int oldIdx = open.IndexOf(newCost);
								if (oldIdx >= 0) {
									if (newGCost < open[oldIdx].gCost) {
										open.RemoveAt(oldIdx);
										open.Add(newCost);

									}
								} else {
									if(open.Count < open.Capacity) {
										open.Add(newCost);
									} else {
										return;
									}
								}
							}
						}
					}
				}//while end

				while (!math.all(currentNode.idx == currentNode.origin)) {
					result.Add(currentNode.idx);
					if (!closed.TryGetValue(currentNode.origin, out NodeCost next)) {
						return;
					}
					currentNode = next;
				}

			}//execute end


			private int NodeDistance (int2 nodeA, int2 nodeB) {
				int2 d = nodeA - nodeB;
				int distx = math.abs(d.x);
				int disty = math.abs(d.y);

				if (distx > disty)
					return 14 * disty + 10 * (distx - disty);
				else
					return 14 * distx + 10 * (disty - distx);
			}

		}


		private void Awake () {
			instance = this;
		}

		private void Update () {
			framesProcessed++;

			if (currentRequest != null) {
				//jobHandle.Complete();

				if (jobHandle.IsCompleted || framesProcessed > 3) {
					jobHandle.Complete();

					//make path
					Path path = new Path();

					if (job.result.Length == 0 || Vector3.Distance(currentRequest.dst, job.grid.GetNodePosition(job.result[0])) > 3) {
						path.failed = true;
					} else {
						path.nodes = new List<Vector3>(job.result.Length);

						for (int i = job.result.Length - 1; i >= 0; i--) {
							path.nodes.Add(job.grid.GetNodePosition(job.result[i].x, job.result[i].y));
						}
					}

					currentRequest.result = path;
					currentRequest.done = true;

					//Dispos job structs
					job.grid.Dispose();
					job.result.Dispose();
					job.open.Dispose();
					job.closed.Dispose();
					currentRequest = null;
				}
			}

			//Queue a new job if there are requests
			if (currentRequest == null && requests.Count > 0 && this.grid.nodeSize > 0) {
				currentRequest = requests.Dequeue();

				job = new ProcessPathJob() {
					srcPosition = currentRequest.src,
					dstPosition = currentRequest.dst,
					grid = grid.Copy(Allocator.TempJob),
					result = new NativeList<int2>(Allocator.TempJob),
					open = new NativeBinaryHeap<ProcessPathJob.NodeCost>((int)(grid.width * grid.height / (grid.nodeSize) / 2), Allocator.TempJob),
					closed = new NativeHashMap<int2, ProcessPathJob.NodeCost>(128, Allocator.TempJob)
				};
				jobHandle = job.Schedule();


				framesProcessed = 0;
			}
		}

		public void QueueJob (PathFindingRequest request) {
			requests.Enqueue(request);
		}

		public void UpdateGrid (PathfindingGrid grid) {
			this.grid.Dispose();

			if (grid.nodeSize > 0) {
				this.grid = grid;
			}	
		}

		private void OnDestroy () {
			jobHandle.Complete();
			job.grid.Dispose();

			if (job.result.IsCreated)
				job.result.Dispose();
			if (job.open.items.IsCreated)
				job.open.Dispose();
			if (job.closed.IsCreated)
				job.closed.Dispose();

			this.grid.Dispose();
		}
	}
}