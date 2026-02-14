using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ThiccTapeman.Bezier
{
    [System.Serializable]
    public class CubicBezier
    {
        public Vector3 pointA;
        public Vector3 handleA;
        public Vector3 pointB;
        public Vector3 handleB;

        public Vector3 CalculateCubicBezierPoint(float t)
        {
            Vector3 p = CalculateCubicBezierPoint(this, t);

            return p;
        }

        private static Vector3 CalculateCubicBezierPoint(CubicBezier cubicBezier, float t)
        {
            float u = 1 - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            Vector3 p = uuu * cubicBezier.pointA;
            p += 3 * uu * t * cubicBezier.handleA;
            p += 3 * u * tt * cubicBezier.handleB;
            p += ttt * cubicBezier.pointB;

            return p;
        }






        public Vector3 CalculateCubicBezierPointNormalOffset(float t, float offset)
        {
            Vector3 offsetPosition = CalculateCubicBezierPointNormalOffset(this, t, offset);

            return offsetPosition;
        }


        private static Vector3 CalculateCubicBezierPointNormalOffset(CubicBezier cubicBezier, float t, float offset)
        {
            Vector3 point = CalculateCubicBezierPoint(cubicBezier, t);
            Vector3 normalOffset = CalculateCubicBezierTangent(cubicBezier, t) * offset;

            return point + normalOffset;
        }

        private static Vector3 CalculateCubicBezierTangent(CubicBezier cubicBezier, float t)
        {
            float u = 1 - t;
            float tt = t * t;
            float uu = u * u;

            Vector3 tangent = -3 * uu * cubicBezier.pointA;
            tangent += 3 * (uu - 2 * u * t) * cubicBezier.handleA;
            tangent += 3 * (t * (2 * u - t)) * cubicBezier.handleB;
            tangent += 3 * tt * cubicBezier.pointB;

            return tangent;
        }








        public void DrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(pointA, handleA);
            Gizmos.DrawLine(handleB, pointB);

            Gizmos.color = Color.green;
            Vector3 lastPoint = pointA;
            for (float t = 0.05f; t <= 1; t += 0.05f)
            {
                Vector3 point = CalculateCubicBezierPoint(t);
                Gizmos.DrawLine(lastPoint, point);
                lastPoint = point;
            }
        }








        public static Vector3 CalculateCubicBezierListPoint(List<CubicBezier> list, float t)
        {
            int index = Mathf.FloorToInt(t);

            return CalculateCubicBezierPoint(list[index], t - index);
        }

        public static Vector3 CalculateCubicBezierListPointNormalOffset(List<CubicBezier> list, float t, float offset)
        {
            int index = Mathf.FloorToInt(t);

            return CalculateCubicBezierPointNormalOffset(list[index], t - index, offset);
        }





        public static Vector3 GetStartPoint(List<CubicBezier> list)
        {
            return GetStartPoint(list, out Vector3 normal);
        }

        public static Vector3 GetStartPoint(List<CubicBezier> list, out Vector3 normal)
        {
            normal = CalculateCubicBezierTangent(list[0], 0);

            return list[0].pointA;
        }

        public static Vector3 GetEndPoint(List<CubicBezier> list)
        {
            return GetEndPoint(list, out Vector3 normal);
        }

        public static Vector3 GetEndPoint(List<CubicBezier> list, out Vector3 normal)
        {
            normal = CalculateCubicBezierTangent(list[list.Count - 1], 1);

            return list[list.Count - 1].pointB;
        }
    }
}

