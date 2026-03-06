using UnityEngine;

namespace OniRepl.Words
{
    public class GotoWord : IWord
    {
        public string Name => "goto";
        public string Help => "goto — Move camera to current position. E.g.: 45,82 goto";
        public bool SuppressAchievements => false;

        public string Execute()
        {
            int cell = Registers.Cell;
            if (!Grid.IsValidCell(cell))
                return "Error: no position set (use cursor, x,y first)";

            var pos = Grid.CellToPosCCC(cell, Grid.SceneLayer.Move);
            CameraController.Instance.SetTargetPos(pos, CameraController.Instance.targetOrthographicSize, true);

            Grid.CellToXY(cell, out int x, out int y);
            return $"Camera moved to ({x},{y})";
        }
    }
}
