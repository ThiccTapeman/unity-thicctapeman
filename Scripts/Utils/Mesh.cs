using System.Collections.Generic;
using UnityEngine;

namespace ThiccTapeman.Utils
{
    public static class Mesh
    {

        public static UnityEngine.Mesh CreateMesh(Vector3[] vertices, int[] triangles)
        {
            UnityEngine.Mesh mesh = new UnityEngine.Mesh();

            mesh.vertices = vertices;
            mesh.triangles = triangles;

            mesh.RecalculateNormals();

            return mesh;
        }

        public static UnityEngine.Mesh BoxCutout(UnityEngine.Mesh mesh, Vector3 start, Vector3 end, Transform meshTransform)
        {
            Vector3 min = Vector3.Min(start, end);
            Vector3 max = Vector3.Max(start, end);

            Vector3[] originalVertices = mesh.vertices;
            Vector3[] originalNormals = mesh.normals;
            Vector2[] originalUVs = mesh.uv;
            int[] originalTriangles = mesh.triangles;

            List<Vector3> newVertices = new List<Vector3>();
            List<Vector3> newNormals = new List<Vector3>();
            List<Vector2> newUVs = new List<Vector2>();
            List<int> newTriangles = new List<int>();

            Dictionary<int, int> vertexMap = new Dictionary<int, int>();

            // Step 1: Transform each vertex to world space and categorize it based on cutout box
            List<Vector3> worldVertices = new List<Vector3>();
            for (int i = 0; i < originalVertices.Length; i++)
            {
                worldVertices.Add(meshTransform.TransformPoint(originalVertices[i]));
            }

            // Step 2: Add vertices and edges that fall outside the cutout
            for (int i = 0; i < originalTriangles.Length; i += 3)
            {
                int i0 = originalTriangles[i];
                int i1 = originalTriangles[i + 1];
                int i2 = originalTriangles[i + 2];

                bool v0Inside = IsInsideCutout(worldVertices[i0], min, max);
                bool v1Inside = IsInsideCutout(worldVertices[i1], min, max);
                bool v2Inside = IsInsideCutout(worldVertices[i2], min, max);

                int[] indices = { i0, i1, i2 };
                bool[] insideFlags = { v0Inside, v1Inside, v2Inside };

                if (!insideFlags[0] && !insideFlags[1] && !insideFlags[2])
                {
                    // All vertices are outside the cutout, add the whole triangle
                    AddTriangle(newVertices, newNormals, newUVs, newTriangles, originalVertices, originalNormals, originalUVs, indices, vertexMap);
                }
                else if (insideFlags[0] || insideFlags[1] || insideFlags[2])
                {
                    // Partially inside, partially outside: split the triangle and create new vertices on edges
                    List<Vector3> newVerticesForTriangle = new List<Vector3>();
                    List<int> edgeIndices = new List<int>();

                    for (int j = 0; j < 3; j++)
                    {
                        int current = indices[j];
                        int next = indices[(j + 1) % 3];

                        // Add the outside vertex to the new mesh
                        if (!insideFlags[j])
                        {
                            AddVertex(newVertices, newNormals, newUVs, originalVertices[current], originalNormals[current], originalUVs[current], vertexMap, current);
                            newVerticesForTriangle.Add(originalVertices[current]);
                            edgeIndices.Add(vertexMap[current]);
                        }

                        // If edge crosses the cutout boundary, create a new intersection vertex
                        if (insideFlags[j] != insideFlags[(j + 1) % 3])
                        {
                            Vector3 intersect = FindIntersection(worldVertices[current], worldVertices[next], min, max);
                            Vector3 intersectLocal = meshTransform.InverseTransformPoint(intersect);

                            int newVertexIndex = newVertices.Count;
                            newVertices.Add(intersectLocal);
                            newNormals.Add(Vector3.Lerp(originalNormals[current], originalNormals[next], 0.5f));
                            newUVs.Add(Vector2.Lerp(originalUVs[current], originalUVs[next], 0.5f));
                            edgeIndices.Add(newVertexIndex);
                        }
                    }

                    // Create triangles for the new cutout edges
                    for (int k = 1; k < edgeIndices.Count - 1; k++)
                    {
                        newTriangles.Add(edgeIndices[0]);
                        newTriangles.Add(edgeIndices[k]);
                        newTriangles.Add(edgeIndices[k + 1]);
                    }
                }
            }

            // Step 3: Assign the new data to the mesh
            mesh.Clear();
            mesh.vertices = newVertices.ToArray();
            mesh.normals = newNormals.ToArray();
            mesh.uv = newUVs.ToArray();
            mesh.triangles = newTriangles.ToArray();

            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        // Helper method to determine if a vertex is inside the cutout box
        private static bool IsInsideCutout(Vector3 vertex, Vector3 min, Vector3 max)
        {
            return vertex.x >= min.x && vertex.x <= max.x &&
                   vertex.y >= min.y && vertex.y <= max.y &&
                   vertex.z >= min.z && vertex.z <= max.z;
        }

        // Helper method to add a vertex and map it in vertexMap
        private static void AddVertex(List<Vector3> newVertices, List<Vector3> newNormals, List<Vector2> newUVs,
            Vector3 vertex, Vector3 normal, Vector2 uv, Dictionary<int, int> vertexMap, int index)
        {
            if (!vertexMap.ContainsKey(index))
            {
                vertexMap[index] = newVertices.Count;
                newVertices.Add(vertex);
                newNormals.Add(normal);
                newUVs.Add(uv);
            }
        }

        // Helper method to add a triangle
        private static void AddTriangle(List<Vector3> newVertices, List<Vector3> newNormals, List<Vector2> newUVs,
            List<int> newTriangles, Vector3[] vertices, Vector3[] normals, Vector2[] uvs, int[] indices, Dictionary<int, int> vertexMap)
        {
            foreach (int index in indices)
            {
                AddVertex(newVertices, newNormals, newUVs, vertices[index], normals[index], uvs[index], vertexMap, index);
                newTriangles.Add(vertexMap[index]);
            }
        }

        // Method to find the intersection of an edge with the cutout bounding box
        private static Vector3 FindIntersection(Vector3 p1, Vector3 p2, Vector3 min, Vector3 max)
        {
            // Logic to find the intersection point between handleA-handleB and the bounding box (min, max)
            // This involves solving for intersections of the line segment with each box face

            // For simplicity, we can approximate by interpolating along each axis individually
            float t = 0.5f; // Replace this with the correct interpolation factor based on bounding box faces
            return Vector3.Lerp(p1, p2, t);
        }



    }
}

