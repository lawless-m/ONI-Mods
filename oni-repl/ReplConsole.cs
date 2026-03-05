using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace OniRepl
{
    public class ReplConsole : MonoBehaviour
    {
        public static bool IsVisible { get; private set; }
        public static ReplConsole Instance { get; private set; }

        private readonly ForthEngine engine = new ForthEngine();
        private string inputText = "";
        private string outputLog = "";
        private readonly List<string> history = new List<string>();
        private int historyIndex = -1;
        private Vector2 scrollPos;
        private bool focusInput;
        private const int MaxOutputLines = 500;
        private const string InputControlName = "OniReplInput";

        private GUIStyle boxStyle;
        private GUIStyle labelStyle;
        private GUIStyle inputStyle;

        private void Awake()
        {
            Instance = this;
            engine.RegisterWord(new Words.HelpWord(engine));
            engine.RegisterWord(new Words.SpawnWord());
            engine.RegisterWord(new Words.PriorityWord());
            engine.RegisterWord(new Words.BuildWord());
            engine.RegisterWord(new Words.BuiltWord());
            engine.RegisterWord(new Words.FillWord());
            engine.RegisterWord(new Words.WaitWord(engine));
            engine.RegisterWord(new Words.ClearWord(engine));
            engine.RegisterWord(new Words.InfoWord());
            engine.RegisterWord(new Words.ListWord());
            engine.RegisterWord(new Words.DirectionWord("left", -1, 0));
            engine.RegisterWord(new Words.DirectionWord("right", 1, 0));
            engine.RegisterWord(new Words.DirectionWord("up", 0, 1));
            engine.RegisterWord(new Words.DirectionWord("down", 0, -1));
            engine.RegisterWord(new Words.DigWord());
            engine.RegisterWord(new Words.DupWord());
            engine.RegisterWord(new Words.DropWord());
            engine.RegisterWord(new Words.SwapWord());
            engine.RegisterWord(new Words.RotWord());
            engine.RegisterWord(new Words.PrintStackWord());
            engine.RegisterWord(new Words.ResetWord());
            engine.RegisterWord(new Words.SaveWord(engine));
            engine.RegisterWord(new Words.LoadWord(engine));
            engine.RegisterWord(new Words.FontSizeWord());

            AutoLoadInit();
        }

        private void AutoLoadInit()
        {
            var path = Path.Combine(Words.SaveWord.ModDirectory, "init.forth");
            if (!File.Exists(path)) return;

            try
            {
                var content = File.ReadAllText(path);
                var result = engine.Execute(content);
                if (result != null)
                    AppendOutput(result);
            }
            catch (System.Exception ex)
            {
                AppendOutput($"Error loading init.forth: {ex.Message}");
            }
        }

        private void Update()
        {
            if (!IsVisible && Input.GetKeyDown(KeyCode.BackQuote))
            {
                IsVisible = true;
                focusInput = true;
            }
        }

        private int currentFontSize;

        public void SetFontSize(int size)
        {
            currentFontSize = size;
            if (labelStyle != null) labelStyle.fontSize = size;
            if (inputStyle != null) inputStyle.fontSize = size;
        }

        private void EnsureStyles()
        {
            if (boxStyle != null) return;

            currentFontSize = Mathf.RoundToInt(14f * Screen.height / 1080f);

            boxStyle = new GUIStyle(GUI.skin.box);
            var bgTex = new Texture2D(1, 1);
            bgTex.SetPixel(0, 0, new Color(0.1f, 0.1f, 0.12f, 0.92f));
            bgTex.Apply();
            boxStyle.normal.background = bgTex;

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = currentFontSize,
                wordWrap = true,
                richText = true
            };
            labelStyle.normal.textColor = new Color(0.8f, 0.9f, 0.8f);

            inputStyle = new GUIStyle(GUI.skin.textField) { fontSize = currentFontSize };
            inputStyle.normal.textColor = Color.green;
        }

        private void OnGUI()
        {
            if (!IsVisible) return;

            // Handle toggle/close before anything else — must catch these
            // in OnGUI because the catch-all Use() at the bottom eats them
            // before Input.GetKeyDown can see them
            if (Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.BackQuote || Event.current.keyCode == KeyCode.Escape)
                {
                    IsVisible = false;
                    Event.current.Use();
                    return;
                }
            }

            EnsureStyles();

            float height = Screen.height * 0.4f;
            float y = Screen.height - height;
            var consoleRect = new Rect(0, y, Screen.width, height);

            GUI.Box(consoleRect, "", boxStyle);

            GUILayout.BeginArea(new Rect(10, y + 5, Screen.width - 20, height - 10));

            // Header
            GUILayout.Label("<b>ONI REPL</b>  <size=11>(` to toggle, Esc to close)</size>", labelStyle);

            // Output area
            scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));
            GUILayout.Label(outputLog, labelStyle);
            GUILayout.EndScrollView();

            // Input line
            GUILayout.BeginHorizontal();
            GUILayout.Label("> ", labelStyle, GUILayout.Width(20));

            // Handle history navigation before the text field
            if (Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.UpArrow && history.Count > 0)
                {
                    if (historyIndex < history.Count - 1)
                        historyIndex++;
                    inputText = history[history.Count - 1 - historyIndex];
                    Event.current.Use();
                }
                else if (Event.current.keyCode == KeyCode.DownArrow)
                {
                    if (historyIndex > 0)
                    {
                        historyIndex--;
                        inputText = history[history.Count - 1 - historyIndex];
                    }
                    else
                    {
                        historyIndex = -1;
                        inputText = "";
                    }
                    Event.current.Use();
                }
            }

            GUI.SetNextControlName(InputControlName);
            inputText = GUILayout.TextField(inputText, inputStyle);

            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            // Focus the input field
            if (focusInput)
            {
                GUI.FocusControl(InputControlName);
                focusInput = false;
            }

            // Handle enter key submission
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return
                && GUI.GetNameOfFocusedControl() == InputControlName)
            {
                if (!string.IsNullOrEmpty(inputText.Trim()))
                {
                    SubmitInput(inputText.Trim());
                    inputText = "";
                    historyIndex = -1;
                }
                Event.current.Use();
            }

            // Block all input from reaching the game
            if (Event.current.type != EventType.Repaint && Event.current.type != EventType.Layout)
            {
                Event.current.Use();
            }
        }

        private void SubmitInput(string input)
        {
            AppendOutput($"> {input}");
            history.Add(input);

            var result = engine.Execute(input);
            if (result != null)
                AppendOutput(result);

            // Auto-scroll to bottom
            scrollPos = new Vector2(0, float.MaxValue);
        }

        public void PostNotification(string text)
        {
            AppendOutput(text);
            scrollPos = new Vector2(0, float.MaxValue);
        }

        public void ResumeEngine()
        {
            if (!engine.Suspended) return;
            var result = engine.Resume();
            if (result != null)
                AppendOutput(result);
            scrollPos = new Vector2(0, float.MaxValue);
        }

        private void AppendOutput(string text)
        {
            if (string.IsNullOrEmpty(outputLog))
                outputLog = text;
            else
                outputLog += "\n" + text;

            // Trim to max lines
            var lines = outputLog.Split('\n');
            if (lines.Length > MaxOutputLines)
            {
                outputLog = string.Join("\n", lines, lines.Length - MaxOutputLines, MaxOutputLines);
            }
        }
    }
}
