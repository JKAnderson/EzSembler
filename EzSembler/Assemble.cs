using SoulsFormats;
using System;
using System.Linq;

namespace EzSemble
{
    using static Common;

    public static partial class EzSembler
    {
        public static byte[] Assemble(string plaintext)
        {
            BinaryWriterEx bw = new BinaryWriterEx(false);
            int current = 0;
            int next = 0;

            while (current < plaintext.Length)
            {
                next = current + 1;
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
                            bw.WriteByte((byte)(value + 64));
                        }
                        else
                        {
                            bw.WriteByte(0x82);
                            bw.WriteInt32((int)value);
                        }
                    }
                    else if (value == (float)value)
                    {
                        bw.WriteByte(0x80);
                        bw.WriteSingle((float)value);
                    }
                    else
                    {
                        bw.WriteByte(0x81);
                        bw.WriteDouble(value);
                    }
                }
                else if (plaintext[current] == '"')
                {
                    while (next < plaintext.Length && plaintext[next] != '"')
                        next++;

                    if (next == plaintext.Length)
                        throw new Exception("Unclosed string literal");

                    string value = plaintext.Substring(current + 1, next - current - 1);
                    if (value.Contains('\r') || value.Contains('\n'))
                        throw new Exception("String literals may not contain newlines");

                    bw.WriteByte(0xA5);
                    bw.WriteUTF16(value, true);

                    next++;
                }
                else if (plaintext[current] == '+')
                {
                    bw.WriteByte(BytesByOperator["+"]);
                }
                else if (plaintext[current] == '*')
                {
                    bw.WriteByte(BytesByOperator["*"]);
                }
                else if (plaintext[current] == '/')
                {
                    bw.WriteByte(BytesByOperator["/"]);
                }
                else if (plaintext[current] == '<')
                {
                    if (next < plaintext.Length && plaintext[next] == '=')
                    {
                        bw.WriteByte(BytesByOperator["<="]);
                        next++;
                    }
                    else
                    {
                        bw.WriteByte(BytesByOperator["<"]);
                    }
                }
                else if (plaintext[current] == '>')
                {
                    if (next < plaintext.Length && plaintext[next] == '[')
                    {
                        if (next + 2 >= plaintext.Length || plaintext[next + 2] != ']')
                            throw new Exception("Malformed register storage");
                        if (!"01234567".Contains(plaintext[next + 1]))
                            throw new Exception("Register must be from 0-7");

                        bw.WriteByte((byte)(0xA7 + byte.Parse(plaintext[next + 1].ToString())));
                        next += 3;
                    }
                    else if (next < plaintext.Length && plaintext[next] == '=')
                    {
                        bw.WriteByte(BytesByOperator[">="]);
                        next++;
                    }
                    else
                    {
                        bw.WriteByte(BytesByOperator[">"]);
                    }
                }
                else if (plaintext[current] == '=')
                {
                    if (next == plaintext.Length || plaintext[next] != '=')
                        throw new Exception("Orphaned = found");

                    bw.WriteByte(BytesByOperator["=="]);
                    next++;
                }
                else if (plaintext[current] == '!')
                {
                    if (next == plaintext.Length || plaintext[next] != '=')
                        throw new Exception("Orphaned ! found");

                    bw.WriteByte(BytesByOperator["!="]);
                    next++;
                }
                else if (plaintext[current] == '&')
                {
                    if (next == plaintext.Length || plaintext[next] != '&')
                        throw new Exception("Orphaned & found");

                    bw.WriteByte(BytesByOperator["&&"]);
                    next++;
                }
                else if (plaintext[current] == '|')
                {
                    if (next == plaintext.Length || plaintext[next] != '|')
                        throw new Exception("Orphaned | found");

                    bw.WriteByte(BytesByOperator["||"]);
                    next++;
                }
                else if (plaintext[current] == '(')
                {
                    if (next + 1 >= plaintext.Length || plaintext[next + 1] != ')')
                        throw new Exception("Unclosed function call");
                    if (!"0123456".Contains(plaintext[next]))
                        throw new Exception("Function call must take 0-6 arguments");

                    bw.WriteByte((byte)(0x84 + byte.Parse(plaintext[next].ToString())));
                    next += 2;
                }
                else if (plaintext[current] == '[')
                {
                    if (next + 2 >= plaintext.Length || plaintext[next + 1] != ']' || plaintext[next + 2] != '>')
                        throw new Exception("Malformed register retrieval");
                    if (!"01234567".Contains(plaintext[next]))
                        throw new Exception("Register must be from 0-7");

                    bw.WriteByte((byte)(0xAF + byte.Parse(plaintext[next].ToString())));
                    next += 3;
                }
                else if (BytesByTerminator.ContainsKey(plaintext[current]))
                {
                    bw.WriteByte(BytesByTerminator[plaintext[current]]);
                }
                else if (plaintext[current] == '#')
                {
                    if (next + 1 >= plaintext.Length)
                        throw new Exception("Hex literal too short");

                    bw.WriteByte(Convert.ToByte(plaintext.Substring(current + 1, 2), 16));
                    next += 2;
                }
                else if (!char.IsWhiteSpace(plaintext[current]))
                {

                }
                current = next;
            }

            bw.WriteByte(0xA1);
            return bw.FinishBytes();
        }
    }
}
