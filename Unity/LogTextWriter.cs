using System;
using System.IO;
using System.Text;

using UnityEngine;

namespace FakeServer.Unity
{
    internal class LogTextWriter : TextWriter
    {
        WeakReference<TextWriter> OldWriter;
        internal LogTextWriter()
        {

        }
        ~LogTextWriter()
        {
            Disable();
        }

        // 利用設定が有効になっているか？
        public bool IsEnable { get { return (OldWriter != null); } }
        // 現在コンソールに設定されているか？
        public bool IsActive { get { return (Console.Out == this); } }

        public bool SetEnable(bool isEnable)
        {
            return (isEnable) ? Enable() : Disable();
        }
        bool Enable()
        {
            if (OldWriter == null)
            {
                OldWriter = new WeakReference<TextWriter>(Console.Out);
                Console.SetOut(this);
                return true;
            }
            return false;
        }

        bool Disable()
        {
            if (OldWriter != null)
            {
                if (Console.Out == this)
                {
                    TextWriter writer;
                    if (OldWriter.TryGetTarget(out writer))
                    {
                        Console.SetOut(writer);
                    }
                }
                OldWriter = null;
                return true;
            }
            return false;
        }


        public override Encoding Encoding { get { return Encoding.UTF8; } }
        public override void Write(string value)
        {
            base.Write(value);
            Debug.Log(value);
        }
        public override void WriteLine(string value)
        {
            Write(value + Environment.NewLine);
        }
    }
}