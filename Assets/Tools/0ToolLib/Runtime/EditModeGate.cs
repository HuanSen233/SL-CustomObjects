using System;

namespace ToolLib
{
    /// <summary>
    /// Coordinates mutually-exclusive edit mode across tools (curve / triangle). When one tool turns its
    /// edit mode on, any other tool currently owning it is turned off first — so only one tool is ever
    /// editing the scene at a time. Each tool passes a "turnOff" callback that disables its own edit mode.
    /// 协调各工具（曲线/三角面）互斥的编辑模式：一个工具开启编辑模式时，先把当前占用者关掉，
    /// 保证同一时刻只有一个工具在编辑场景。各工具传入一个"turnOff"回调用于关闭自身的编辑模式。
    /// </summary>
    public static class EditModeGate
    {
        private static string _currentId;
        private static Action _currentTurnOff;

        /// <summary>
        /// Request edit-mode activity for a tool. When turning on, any other owner's turnOff callback is
        /// invoked first (mutual exclusion); when the tool is the current owner and turns off, ownership clears.
        /// 请求某工具的编辑模式状态。开启时先调用其他占用者的 turnOff 回调（互斥）；
        /// 当该工具是当前占用者且关闭时，清除占用。
        /// </summary>
        public static void Request(string toolId, bool on, Action turnOffCurrent)
        {
            if (on)
            {
                if (_currentId != null && _currentId != toolId)
                    _currentTurnOff?.Invoke();
                _currentId = toolId;
                _currentTurnOff = turnOffCurrent;
            }
            else if (_currentId == toolId)
            {
                _currentId = null;
                _currentTurnOff = null;
            }
        }
    }
}
