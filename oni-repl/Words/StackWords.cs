using System.Collections.Generic;
using System.Linq;

namespace OniRepl.Words
{
    public class DupWord : IWord
    {
        public string Name => "dup";
        public string Help => "a dup — Duplicate top of stack";
        public bool SuppressAchievements => false;
        public string Execute(Stack<StackValue> stack)
        {
            var top = stack.Peek();
            stack.Push(top);
            return null;
        }
    }

    public class DropWord : IWord
    {
        public string Name => "drop";
        public string Help => "a drop — Discard top of stack";
        public bool SuppressAchievements => false;
        public string Execute(Stack<StackValue> stack)
        {
            stack.Pop();
            return null;
        }
    }

    public class SwapWord : IWord
    {
        public string Name => "swap";
        public string Help => "a b swap — Swap top two stack values";
        public bool SuppressAchievements => false;
        public string Execute(Stack<StackValue> stack)
        {
            var a = stack.Pop();
            var b = stack.Pop();
            stack.Push(a);
            stack.Push(b);
            return null;
        }
    }

    public class RotWord : IWord
    {
        public string Name => "rot";
        public string Help => "a b c rot — Rotate third to top ( a b c -- b c a )";
        public bool SuppressAchievements => false;
        public string Execute(Stack<StackValue> stack)
        {
            var c = stack.Pop();
            var b = stack.Pop();
            var a = stack.Pop();
            stack.Push(b);
            stack.Push(c);
            stack.Push(a);
            return null;
        }
    }

    public class ResetWord : IWord
    {
        public string Name => "reset";
        public string Help => "reset — Clear the entire stack";
        public bool SuppressAchievements => false;
        public string Execute(Stack<StackValue> stack)
        {
            int count = stack.Count;
            stack.Clear();
            return count > 0 ? $"Cleared {count} item(s) from stack" : "Stack already empty";
        }
    }

    public class PrintStackWord : IWord
    {
        public string Name => ".s";
        public string Help => ".s — Print stack contents (non-destructive)";
        public bool SuppressAchievements => false;
        public string Execute(Stack<StackValue> stack)
        {
            if (stack.Count == 0)
                return "Stack empty";
            var items = stack.Reverse().Select(v => v.ToString());
            return $"Stack ({stack.Count}): {string.Join(" ", items)}";
        }
    }
}
