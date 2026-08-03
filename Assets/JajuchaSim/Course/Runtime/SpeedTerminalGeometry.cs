using UnityEngine;

namespace JajuchaSim.Course
{
    /// <summary>
    /// Pure geometry helpers for speed measurement terminals.
    /// Each terminal is a line snapped to a grid cell edge (optionally multi-tile wide).
    /// </summary>
    public static class SpeedTerminalGeometry
    {
        /// <summary>
        /// World-space endpoints of a terminal line on the XZ plane (Y = 0).
        /// Width extends along the edge direction.
        /// </summary>
        public static void GetLineEndpoints(
            int cellX,
            int cellZ,
            GridEdge edge,
            int widthTiles,
            float tileSizeCm,
            out Vector3 p0,
            out Vector3 p1)
        {
            int w = widthTiles < 1 ? 1 : widthTiles;
            float ts = tileSizeCm > 0f ? tileSizeCm : 20f;

            float x0 = cellX * ts;
            float z0 = cellZ * ts;

            switch (edge)
            {
                case GridEdge.South:
                    // South edges of cells (cellX .. cellX+w-1, cellZ)
                    p0 = new Vector3(x0, 0f, z0);
                    p1 = new Vector3(x0 + w * ts, 0f, z0);
                    break;
                case GridEdge.East:
                    // East edges of cells (cellX, cellZ .. cellZ+w-1)
                    p0 = new Vector3(x0 + ts, 0f, z0);
                    p1 = new Vector3(x0 + ts, 0f, z0 + w * ts);
                    break;
                case GridEdge.West:
                    p0 = new Vector3(x0, 0f, z0);
                    p1 = new Vector3(x0, 0f, z0 + w * ts);
                    break;
                case GridEdge.North:
                default:
                    // North edges of cells (cellX .. cellX+w-1, cellZ)
                    p0 = new Vector3(x0, 0f, z0 + ts);
                    p1 = new Vector3(x0 + w * ts, 0f, z0 + ts);
                    break;
            }
        }

        public static void GetLineEndpoints(TriggerInstance terminal, float tileSizeCm, out Vector3 p0, out Vector3 p1)
        {
            GetLineEndpoints(
                terminal.CellX,
                terminal.CellZ,
                terminal.Edge,
                terminal.WidthTiles,
                tileSizeCm,
                out p0,
                out p1);
        }

        public static Vector3 GetLineMidpoint(TriggerInstance terminal, float tileSizeCm)
        {
            GetLineEndpoints(terminal, tileSizeCm, out var p0, out var p1);
            return (p0 + p1) * 0.5f;
        }

        /// <summary>
        /// Distance between two terminals from their actual grid/world line midpoints (cm).
        /// </summary>
        public static float DistanceCm(TriggerInstance a, TriggerInstance b, float tileSizeCm)
        {
            if (a == null || b == null) return 0f;
            var ma = GetLineMidpoint(a, tileSizeCm);
            var mb = GetLineMidpoint(b, tileSizeCm);
            return Vector3.Distance(ma, mb);
        }
    }
}
