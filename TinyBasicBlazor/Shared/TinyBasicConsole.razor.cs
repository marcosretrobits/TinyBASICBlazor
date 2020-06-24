using com.thisiscool.tinybasic;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TinyBasicBlazor.Shared
{
    // TODO: debug
    // TODO: query string params: https://chrissainty.com/working-with-query-strings-in-blazor/

    /// <summary>
    /// TinyBasic console control.
    /// </summary>
    public partial class TinyBasicConsole
    {
        private class TinyBasicConsoleIO : IConsoleIO
        {
            private readonly TinyBasicConsole tinyBasicConsole;

            public TinyBasicConsoleIO(TinyBasicConsole tinyBasicConsole)
            {
                this.tinyBasicConsole = tinyBasicConsole;
            }

            public void screenChar(char ch)
            {
                tinyBasicConsole.Write(ch);
            }

            public void print(string theMsg)
            {
                tinyBasicConsole.Write(theMsg);
            }

            public char read()
            {
                return tinyBasicConsole.Read();
            }
        }

        private class ControlCharacter
        {
            public const char Null = '\0';
            public const char BackSpace = '\b';
            // Used to clear console output
            public const char FormFeed = (char)12;
            // Used to enable input echo
            public const char DeviceControl2 = (char)18;
            // Used to disable input echo
            public const char DeviceControl4 = (char)20;
        }

        /// <summary>
        /// Id
        /// </summary>
        /// <remarks>Supplied by parent component</remarks>
        [Parameter]
        public string Id { get; set; }

        /// <summary>
        /// TextArea Rows
        /// </summary>
        /// <remarks>Supplied by parent component</remarks>
        [Parameter]
        public int Rows { get; set; }

        /// <summary>
        /// TextArea Columns
        /// </summary>
        /// <remarks>Supplied by parent component</remarks>
        [Parameter]
        public int Cols { get; set; }

        /// <summary>
        /// TextArea content
        /// </summary>
        public string Text { get; set; }

        public bool echo;

        private TinyBasic tinyBasic;

        private int lastReturnCode;

        private readonly Timer timer;

        private Queue<char> inputBuffer;
        private object inputBufferSync = new object();

        bool mustRefreshOutput;

        private void OnKeyDownHandler(KeyboardEventArgs args)
        {
            // Console.WriteLine($"KeyboardEventArgs key: {args.Key} code: {args.Code} type: {args.Type} meta: {args.MetaKey} shift: {args.ShiftKey} ctrl: {args.CtrlKey} repeat: {args.Repeat}");

            if (args.Code.Contains("Backspace"))
            {
                lock (inputBufferSync)
                {
                    if (inputBuffer.Any())
                    {
                        var items = inputBuffer.ToList();
                        items.RemoveAt(items.Count - 1);
                        inputBuffer = new Queue<char>(items);

                        this.Text = this.Text.Substring(0, this.Text.Length - 1);
                        this.mustRefreshOutput = true;
                    }
                }
            }
        }

        private void OnKeyPressHandler(KeyboardEventArgs args)
        {
            // Console.WriteLine($"KeyboardEventArgs key: {args.Key} code: {args.Code} type: {args.Type} meta: {args.MetaKey} shift: {args.ShiftKey} ctrl: {args.CtrlKey} repeat: {args.Repeat}");

            char c = args.Key[0];
            if (args.Code.Contains("Enter"))
            {
                c = '\n';
            }

            EnqueueInput(c);

            // Echo TODO: see NeedsEcho in TinyBasic
            this.Write(c);
        }

        /*
        private void OnInputHandler(ChangeEventArgs e)
        {
            JSRuntime.InvokeVoidAsync("ScrollTextArea", "TinyBasicConsole");
        }
        */

        private async Task UpdateTextArea()
        {
            StateHasChanged();
            await JSRuntime.InvokeVoidAsync("ScrollTextArea", $"{Id}_TextArea");
            await SetFocus();
        }

        public TinyBasicConsole()
        {
            var consoleIO = new TinyBasicConsoleIO(this);
            this.tinyBasic = new TinyBasic(consoleIO);

            this.inputBuffer = new Queue<char>();
            this.mustRefreshOutput = false;

            this.echo = false;

            this.Text = "Welcome to TINY BASIC!\n";

            this.lastReturnCode = 0;
            this.tinyBasic.StartTinyBasic(null);

            this.timer = new Timer(async (state) =>
            {
                if (this.mustRefreshOutput)
                {
                    if (this.Text.Length >= 19200)
                    {
                        this.Text = this.Text.Substring(this.Text.Length - 200, 200);
                    }
                    await UpdateTextArea();
                    this.mustRefreshOutput = false;
                }

                lock (inputBufferSync)
                {
                    if (!inputBuffer.Any() &&
                        (lastReturnCode == TinyBasic.INTERP_NEEDCHAR || lastReturnCode == TinyBasic.INTERP_NEEDLINE))
                    {
                        return;
                    }

                    if (lastReturnCode == TinyBasic.INTERP_NEEDLINE && inputBuffer.ToArray()[inputBuffer.Count - 1] != '\n')
                    {
                        return;
                    }

                    do
                    {
                        this.lastReturnCode = this.tinyBasic.RunTinyBasic(this.lastReturnCode);
                    }
                    while (inputBuffer.Any() && lastReturnCode != TinyBasic.INTERP_TIMESLICE_FINISHED);
                }
            }, null, 0, 100);
        }

        public async Task TypeAndRun(string program, string[] input)
        {
            Break();

            this.Text = "Loading...\n";
            await UpdateTextArea();

            var inputStringBuilder = new StringBuilder();

            inputStringBuilder.AppendLine();
            inputStringBuilder.AppendLine("CLEAR");
            for (var v = 'A'; v <= 'Z'; v++)
            {
                inputStringBuilder.AppendLine($"LET {v}=0");
            }
            inputStringBuilder.AppendLine(program.Trim('\r', '\n'));
            inputStringBuilder.Append(ControlCharacter.FormFeed);
            inputStringBuilder.AppendLine("RUN");
            if (input != null && input.Length > 0)
            {
                //inputStringBuilder.AppendLine(ControlCharacter.DeviceControl2.ToString());
                for (int i=0; i < input.Length; i++)
                {
                    string inputLine = input[i];

                    if (!string.IsNullOrEmpty(inputLine))
                    {
                        if (i == 0)
                        {
                            inputLine = $"{ControlCharacter.DeviceControl2}{inputLine}";
                        }

                        if (i == input.Length - 1)
                        {
                            inputLine = $"{inputLine}{ControlCharacter.DeviceControl4}";
                        }

                        inputStringBuilder.AppendLine(inputLine);
                    }
                }
                //inputStringBuilder.AppendLine(ControlCharacter.DeviceControl4.ToString());
            }

            SetInput(inputStringBuilder.ToString());
        }

        public async Task SetFocus()
        {
            await JSRuntime.InvokeVoidAsync("SetFocus", $"{Id}_TextArea");
        }

        private void Break()
        {
            tinyBasic.setBroken(true);
            SetInput("\n");
        }

        private void Clear()
        {
            this.Text = string.Empty;
            mustRefreshOutput = true;
        }

        private void EnqueueInput(char c)
        {
            lock (inputBufferSync)
            {
                inputBuffer.Enqueue(c);
            }
        }

        private void SetInput(string s)
        {
            lock (inputBufferSync)
            {
                inputBuffer.Clear();
                var chars = s.ToCharArray();
                foreach (char c in chars)
                {
                    inputBuffer.Enqueue(c);
                }
            }
        }

        public char Read()
        {
            lock (inputBufferSync)
            {
                while (true)
                {
                    if (inputBuffer.Any())
                    {
                        var c = inputBuffer.Dequeue();
                        switch (c)
                        {
                            case ControlCharacter.FormFeed:
                                this.Text = string.Empty;
                                break;
                            case ControlCharacter.DeviceControl2:
                                this.echo = true;
                                break;
                            case ControlCharacter.DeviceControl4:
                                this.echo = false;
                                break;

                            default:
                                if (this.echo)
                                {
                                    this.Text += c;
                                }
                                return c;
                        }
                    }
                    else
                    {
                        return ControlCharacter.Null;
                    }
                }
            }
        }

        public void Write(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                this.Text += text;
            }
            //UpdateTextArea();
            this.mustRefreshOutput = true;
        }

        public void Write(char character)
        {
            this.Write(new string(new[] { character }));
        }

    }
}
