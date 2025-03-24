using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Oxide.Plugins
{
    [Info("QRTL", "Quapi", "0.2.1")]
    [Description("A fix for RTL Languages")]
    class QRTL : CovalencePlugin
    {
        private object OnBetterChat(Dictionary<string, object> data)
        {
            var message = data["Message"] as string;
            if (IsRightToLeft(message))
            {
                data["Message"] = RtlText(message);
                return data;
            }
            else
                return null;
        }

        #region -Methods-

        protected string RtlText(string text)
        {
            if (!IsRightToLeft(text)) return text;

            //var reversed = Reverse(text);
            //return IsRTLLang(text) ? reversed.Replace(" ", "") : reversed;

            StringBuilder fixedMessage = new StringBuilder();

            // Keep track of insertion proceedings to retain sentence logic
            // If true, insert at beginning of sentence
            bool resetInsertionPos = false;


            string[] words = text.Split(' ');

            for (int i = 0; i < words.Length; i++)
            {
                if (IsRightToLeft(words[i]))
                {
                    StringBuilder fixedRTLPart = new StringBuilder();

                    for (; i < words.Length; i++)
                    {
                        if (IsRightToLeft(words[i]) || IsNumber(words[i]))
                        {
                            string wordToFix = words[i];
                            fixedRTLPart.Insert(0, FixWord(wordToFix) + ' ');
                        }
                        else
                        {
                            i--;
                            break;
                        }
                    }

                    if (!resetInsertionPos)
                    {
                        fixedMessage.Append(fixedRTLPart);
                    }
                    else
                    {
                        fixedMessage.Insert(0, fixedRTLPart);
                        resetInsertionPos = false;
                    }
                }
                else
                {
                    StringBuilder fixedLTRPart = new StringBuilder();

                    for (; i < words.Length; i++)
                    {
                        if (!IsRightToLeft(words[i]))
                        {
                            fixedLTRPart.Append(words[i]).Append(' ');
                        }
                        else
                        {
                            i--;
                            break;
                        }
                    }
                    resetInsertionPos = true;
                    fixedMessage.Insert(0, fixedLTRPart);
                }
            }

            return fixedMessage.ToString();
        }

        protected bool IsBothRTLOrSpecial(char a, char b)
        {
            return IsRTLLang(a) && IsRTLLang(b)
                    || IsRTLLang(a) && IsSpecialChar(b)
                    || IsSpecialChar(a) && IsRTLLang(b)
                    || IsSpecialChar(a) && IsSpecialChar(b);
        }

        protected bool IsSpecialChar(char character)
        {
            return character == '!' || character == ' ' || character == '-' || character == '_' || character == '@'
                    || character == '#' || character == '$' || character == '%' || character == '^' || character == '&' || character == '*'
                    || character == '?' || character == '(' || character == ')' || character == ';';
        }

        protected bool IsNumber(string v)
        {
            foreach (char c in v.ToCharArray())
            {
                if (!char.IsDigit(c))
                    return false;
            }

            return true;
        }

        protected bool IsRTL(string text)
        {
            return IsRightToLeft(text);
        }

        protected bool IsRTLLang(char c)
        {
            return IsRTL(c + "");
        }

        #region -RTL-

        protected string FixWord(string word)
        {
            char[] chars = word.ToCharArray();

            chars = SwapRTLCharacters(chars);
            chars = SwapWordIndexes(chars);

            return new string(chars);
        }

        /**
         * Swaps all RTL characters and switches their order
         */
        protected char[] SwapRTLCharacters(char[] characters)
        {
            Dictionary<int, char> chars = new Dictionary<int, char>();

            for (int i = 0; i < characters.Length; i++)
            {
                chars.Add(i, characters[i]);
            }

            for (int i = 0; i < chars.Count; i++)
            {
                for (int j = i; j < chars.Count; j++)
                {
                    if (IsBothRTLOrSpecial(chars[i], chars[j]))
                    {
                        char tmp = chars[j];
                        chars[j] = chars[i];
                        chars[i] = tmp;
                    }
                    else 
                    {
                        break;
                    }
                }
            }

            char[] returnable = new char[chars.Count];
            for (int i = 0; i < chars.Count; i++)
                returnable[i] = chars[i];


            return returnable;
        }

        protected char[] SwapWordIndexes(char[] characters)
        {
            if (characters.Length == 0) return new char[0];

            char[] chars = characters;

            Stack<string> innerWords = new Stack<string>();

            StringBuilder currentWord = new StringBuilder();
            foreach (char character in chars) {
                if (currentWord.Length == 0 || IsBothRTLOrSpecial(currentWord[0], character)
                        || !IsRightToLeft(currentWord[0].ToString()) && !IsSpecialChar(currentWord[0])
                        && !IsRightToLeft(character.ToString()) && !IsSpecialChar(character))
                {
                    currentWord.Append(character);
                }
                else
                {
                    innerWords.Push(currentWord.ToString());
                    currentWord = new StringBuilder("" + character);
                }
            }

            if (currentWord.Length > 0)
            {
                innerWords.Push(currentWord.ToString());
            }

            if (innerWords.Count == 0)
            {
                return new char[0];
            }

            int currentIndex = 0;
            while (innerWords.Count != 0)
            {
                string s = innerWords.Pop();
                foreach (char c in s.ToCharArray())
                {
                    chars[currentIndex] = c;
                    currentIndex++;
                }
            }

            return chars;
        }

        protected bool IsRightToLeft(string text)
        {
            foreach (var c in text)
            {
                if (c >= 0x5BE && c <= 0x10B7F)
                {
                    if (c <= 0x85E)
                    {
                        if (c == 0x5BE) return true;
                        else if (c == 0x5C0) return true;
                        else if (c == 0x5C3) return true;
                        else if (c == 0x5C6) return true;
                        else if (0x5D0 <= c && c <= 0x5EA) return true;
                        else if (0x5F0 <= c && c <= 0x5F4) return true;
                        else if (c == 0x608) return true;
                        else if (c == 0x60B) return true;
                        else if (c == 0x60D) return true;
                        else if (c == 0x61B) return true;
                        else if (0x61E <= c && c <= 0x64A) return true;
                        else if (0x66D <= c && c <= 0x66F) return true;
                        else if (0x671 <= c && c <= 0x6D5) return true;
                        else if (0x6E5 <= c && c <= 0x6E6) return true;
                        else if (0x6EE <= c && c <= 0x6EF) return true;
                        else if (0x6FA <= c && c <= 0x70D) return true;
                        else if (c == 0x710) return true;
                        else if (0x712 <= c && c <= 0x72F) return true;
                        else if (0x74D <= c && c <= 0x7A5) return true;
                        else if (c == 0x7B1) return true;
                        else if (0x7C0 <= c && c <= 0x7EA) return true;
                        else if (0x7F4 <= c && c <= 0x7F5) return true;
                        else if (c == 0x7FA) return true;
                        else if (0x800 <= c && c <= 0x815) return true;
                        else if (c == 0x81A) return true;
                        else if (c == 0x824) return true;
                        else if (c == 0x828) return true;
                        else if (0x830 <= c && c <= 0x83E) return true;
                        else if (0x840 <= c && c <= 0x858) return true;
                        else if (c == 0x85E) return true;
                    }
                    else if (c == 0x200F) return true;
                    else if (c >= 0xFB1D)
                    {
                        if (c == 0xFB1D) return true;
                        else if (0xFB1F <= c && c <= 0xFB28) return true;
                        else if (0xFB2A <= c && c <= 0xFB36) return true;
                        else if (0xFB38 <= c && c <= 0xFB3C) return true;
                        else if (c == 0xFB3E) return true;
                        else if (0xFB40 <= c && c <= 0xFB41) return true;
                        else if (0xFB43 <= c && c <= 0xFB44) return true;
                        else if (0xFB46 <= c && c <= 0xFBC1) return true;
                        else if (0xFBD3 <= c && c <= 0xFD3D) return true;
                        else if (0xFD50 <= c && c <= 0xFD8F) return true;
                        else if (0xFD92 <= c && c <= 0xFDC7) return true;
                        else if (0xFDF0 <= c && c <= 0xFDFC) return true;
                        else if (0xFE70 <= c && c <= 0xFE74) return true;
                        else if (0xFE76 <= c && c <= 0xFEFC) return true;
                        else if (0x10800 <= c && c <= 0x10805) return true;
                        else if (c == 0x10808) return true;
                        else if (0x1080A <= c && c <= 0x10835) return true;
                        else if (0x10837 <= c && c <= 0x10838) return true;
                        else if (c == 0x1083C) return true;
                        else if (0x1083F <= c && c <= 0x10855) return true;
                        else if (0x10857 <= c && c <= 0x1085F) return true;
                        else if (0x10900 <= c && c <= 0x1091B) return true;
                        else if (0x10920 <= c && c <= 0x10939) return true;
                        else if (c == 0x1093F) return true;
                        else if (c == 0x10A00) return true;
                        else if (0x10A10 <= c && c <= 0x10A13) return true;
                        else if (0x10A15 <= c && c <= 0x10A17) return true;
                        else if (0x10A19 <= c && c <= 0x10A33) return true;
                        else if (0x10A40 <= c && c <= 0x10A47) return true;
                        else if (0x10A50 <= c && c <= 0x10A58) return true;
                        else if (0x10A60 <= c && c <= 0x10A7F) return true;
                        else if (0x10B00 <= c && c <= 0x10B35) return true;
                        else if (0x10B40 <= c && c <= 0x10B55) return true;
                        else if (0x10B58 <= c && c <= 0x10B72) return true;
                        else if (0x10B78 <= c && c <= 0x10B7F) return true;
                    }
                }
            }
            return false;
        }
        #endregion
        #endregion
    }
}
