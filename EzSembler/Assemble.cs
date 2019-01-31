﻿using System;
using System.Linq;

namespace EzSemble
{
    using System.IO;
    using System.Text;
    using static Common;

    public static partial class EzSembler
    {
        public static byte[] Assemble(string plaintext)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms, Encoding.Unicode))
            {
                int current = 0;
                int next = 0;

                while (current < plaintext.Length)
                {
                    next = current + 1;
                    Parse(plaintext, bw, current, ref next);
                    current = next;
                }

                bw.Write((byte)0xA1);
                return ms.ToArray();
            }
        }

        private static void Parse(string plaintext, BinaryWriter bw, int current, ref int next)
        {
            // Number literal
            if (plaintext[current] == '-' || char.IsDigit(plaintext[current]))
            {
                if (plaintext[current] == '-' && (next == plaintext.Length || !char.IsDigit(plaintext[next])))
                    throw new Exception("Negative sign must be immediately followed by a number");

                while (next < plaintext.Length && char.IsDigit(plaintext[next]))
                    next++;

                if (next + 1 < plaintext.Length && plaintext[next] == '.' && char.IsDigit(plaintext[next + 1]))
                {
                    next++;
                    while (next < plaintext.Length && char.IsDigit(plaintext[next]))
                        next++;
                }

                string str = plaintext.Substring(current, next - current);
                double value = double.Parse(str);
                if (value == Math.Floor(value))
                {
                    if (value >= -64 && value <= 63)
                    {
                        bw.Write((byte)(value + 64));
                    }
                    else
                    {
                        bw.Write((byte)0x82);
                        bw.Write((int)value);
                    }
                }
                else if (value == (float)value)
                {
                    bw.Write((byte)0x80);
                    bw.Write((float)value);
                }
                else
                {
                    bw.Write((byte)0x81);
                    bw.Write(value);
                }
            }
            // String literal
            else if (plaintext[current] == '"')
            {
                while (next < plaintext.Length && plaintext[next] != '"')
                    next++;

                if (next == plaintext.Length)
                    throw new Exception("Unclosed string literal");

                string value = plaintext.Substring(current + 1, next - current - 1);
                if (value.Contains('\r') || value.Contains('\n'))
                    throw new Exception("String literals may not contain newlines");

                bw.Write((byte)0xA5);
                bw.Write(Encoding.Unicode.GetBytes(value + "\0"));

                next++;
            }
            // Add
            else if (plaintext[current] == '+')
            {
                bw.Write(BytesByOperator["+"]);
            }
            // Multiply
            else if (plaintext[current] == '*')
            {
                bw.Write(BytesByOperator["*"]);
            }
            else if (plaintext[current] == '/')
            {
                // Comment
                if (next < plaintext.Length && plaintext[next] == '/')
                {
                    while (next < plaintext.Length && plaintext[next] != '\n')
                        next++;
                    next++;
                }
                // Divide
                else
                {
                    bw.Write(BytesByOperator["/"]);
                }
            }
            else if (plaintext[current] == '<')
            {
                // Less than or equal to
                if (next < plaintext.Length && plaintext[next] == '=')
                {
                    bw.Write(BytesByOperator["<="]);
                    next++;
                }
                // Less than
                else
                {
                    bw.Write(BytesByOperator["<"]);
                }
            }
            else if (plaintext[current] == '>')
            {
                // Set register
                if (next < plaintext.Length && plaintext[next] == '[')
                {
                    if (next + 2 >= plaintext.Length || plaintext[next + 2] != ']')
                        throw new Exception("Malformed register storage");
                    if (!"01234567".Contains(plaintext[next + 1]))
                        throw new Exception("Register must be from 0-7");

                    bw.Write((byte)(0xA7 + byte.Parse(plaintext[next + 1].ToString())));
                    next += 3;
                }
                // Greater than or equal to
                else if (next < plaintext.Length && plaintext[next] == '=')
                {
                    bw.Write(BytesByOperator[">="]);
                    next++;
                }
                // Greater than
                else
                {
                    bw.Write(BytesByOperator[">"]);
                }
            }
            // Equal to
            else if (plaintext[current] == '=')
            {
                if (next == plaintext.Length || plaintext[next] != '=')
                    throw new Exception("Orphaned = found");

                bw.Write(BytesByOperator["=="]);
                next++;
            }
            // Not equal to
            else if (plaintext[current] == '!')
            {
                if (next == plaintext.Length || plaintext[next] != '=')
                    throw new Exception("Orphaned ! found");

                bw.Write(BytesByOperator["!="]);
                next++;
            }
            // Logical and
            else if (plaintext[current] == '&')
            {
                if (next == plaintext.Length || plaintext[next] != '&')
                    throw new Exception("Orphaned & found");

                bw.Write(BytesByOperator["&&"]);
                next++;
            }
            // Logical or
            else if (plaintext[current] == '|')
            {
                if (next == plaintext.Length || plaintext[next] != '|')
                    throw new Exception("Orphaned | found");

                bw.Write(BytesByOperator["||"]);
                next++;
            }
            // Function call
            else if (plaintext[current] == '(')
            {
                if (next + 1 >= plaintext.Length || plaintext[next + 1] != ')')
                    throw new Exception("Unclosed function call");
                if (!"0123456".Contains(plaintext[next]))
                    throw new Exception("Function call must take 0-6 arguments");

                bw.Write((byte)(0x84 + byte.Parse(plaintext[next].ToString())));
                next += 2;
            }
            // Get register
            else if (plaintext[current] == '[')
            {
                if (next + 2 >= plaintext.Length || plaintext[next + 1] != ']' || plaintext[next + 2] != '>')
                    throw new Exception("Malformed register retrieval");
                if (!"01234567".Contains(plaintext[next]))
                    throw new Exception("Register must be from 0-7");

                bw.Write((byte)(0xAF + byte.Parse(plaintext[next].ToString())));
                next += 3;
            }
            // , or .
            else if (BytesByTerminator.ContainsKey(plaintext[current]))
            {
                bw.Write(BytesByTerminator[plaintext[current]]);
            }
            // Unknown opcode
            else if (plaintext[current] == '#')
            {
                if (next + 1 >= plaintext.Length)
                    throw new Exception("Hex literal too short");

                bw.Write(Convert.ToByte(plaintext.Substring(current + 1, 2), 16));
                next += 2;
            }
            // Whitespace
            else if (char.IsWhiteSpace(plaintext[current]))
            {
                while (next < plaintext.Length && char.IsWhiteSpace(plaintext[next]))
                    next++;
            }
            // Uh-oh
            else
            {
                throw new Exception($"Unknown character: {plaintext[current]}");
            }
        }
    }
}
