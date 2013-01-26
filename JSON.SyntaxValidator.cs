/*
 JSON Syntax Validator 
 Based on code from
 * 
 How do I write my own parser? (for JSON)
 By Patrick van Bergen 
 http://techblog.procurios.nl/k/618/news/view/14605/14863/How-do-I-write-my-own-parser-for-JSON.html

 */
using System;
using System.Collections;
using System.Globalization;
using System.Text;
using DynamicSugar;
using System.Collections.Generic;
using JsonParser;

namespace JSON.SyntaxValidator
{
    /// <summary>
    /// This class encodes and decodes JSON strings.
    /// Spec. details, see http://www.json.org/
    ///
    /// JSON uses Arrays and Objects. These correspond here to the datatypes ArrayList and Hashtable.
    /// All numbers are parsed to doubles.
    /// </summary>
    public class Compiler : Tokenizer
    {
        public const string SYNTAX_ERROR_001 = @"Trailing coma not supported";
        public const string SYNTAX_ERROR_002 = @"Missing ']'";
        public const string SYNTAX_ERROR_003 = @"Missing '}'";
        public const string SYNTAX_ERROR_004 = @"Missing ':'";
        public const string SYNTAX_ERROR_005 = @"Missing ','";
        public const string SYNTAX_ERROR_006 = @"Missing '""'";
        public const string SYNTAX_ERROR_007 = @"String expected instead of ID";
        public const string SYNTAX_ERROR_008 = @"Invalid value:";
        public const string SYNTAX_ERROR_009 = @"Expected ',' or ']'";
        public const string SYNTAX_ERROR_010 = @"Expected ',' or '}'";
        public const string SYNTAX_ERROR_011 = @"Missing '}' or '""' expected";
        public const string SYNTAX_ERROR_012 = @"Missing value ',,' is invalid";
        public const string SYNTAX_ERROR_013 = @"',' unexpected";
        public const string SYNTAX_ERROR_014 = @"'{0}' unexpected";
        public const string SYNTAX_ERROR_015 = @"']' unexpected";
        public const string SYNTAX_ERROR_016 = @"'}' unexpected";
        public const string SYNTAX_ERROR_017 = @"'""' missing";

        private char[]  _charArray;
        private int     _index;
        private bool    _supportIDWithNoQuote;
        private bool    _supportTrailingComa;
        private bool    _supportStartComment;

        private const int BUILDER_CAPACITY = 2000;

        /// <summary>
        /// Parses the string json into a value
        /// </summary>
        /// <param name="json">A JSON string.</param>
        /// <returns>An ArrayList, a Hashtable, a double, a string, null, true, or false</returns>
        public object Validate(string json, bool supportStartComment = false, bool relaxMode = false, CommentInfos commentInfos = null)
        {
            bool success = true;
            this._supportStartComment = supportStartComment;

            if (commentInfos == null) // Optimization for the TextHighlighter extension
            {                         // So we do not have parse the comment twice if possible
                commentInfos = new JsonParser.CommentParser().Parse(json);
            }

            _supportIDWithNoQuote = (relaxMode) || (commentInfos.Count > 0 && commentInfos[0].Text.Contains(@"""use relax"""));
            _supportTrailingComa  = _supportIDWithNoQuote;

            return Compile(json, ref success);
        }

        /// <summary>
        /// Parses the string json into a value; and fills 'success' with the successfullness of the parse.
        /// </summary>
        /// <param name="json">A JSON string.</param>
        /// <param name="success">Successful parse?</param>
        /// <returns>An ArrayList, a Hashtable, a double, a string, null, true, or false</returns>
        public object Compile(string json, ref bool success)
        {
            success = true;
            if (json != null)
            {
                this._charArray   = json.ToCharArray();
                this._index       = 0;
                object value      = ParseValue(ref success);
                var tokenAHead    = LookAhead(this._charArray, this._index, this._supportStartComment);
                if (tokenAHead != TOKENS.NONE)
                {
                    this.ThrowError(SYNTAX_ERROR_014.format(TOKEN_STRING[tokenAHead]), this._index);
                }
                return value;
            }
            else
            {
                return null;
            }
        }

        private  int GetLine(int index)
        {
            int lineCounter = 0;
            for (int i = 0; i < index; i++)
            {
                if (this._charArray[i] == '\n')
                    lineCounter++;
            }
            return lineCounter + 1;
        }

        private int GetColumn(int index)
        {
            int i = index;
            if (i >= this._charArray.Length)
            {
                i = this._charArray.Length - 1;
            }
            while (i >= 0 && this._charArray[i] != '\n')
            {
                i--;
            }
            if (i == -1)
            {
                i = 0;
            }
            else
            {
                i++;
            }
            var col = index - i;
            return col;
        }

        private void ThrowError(string message, int index)
        {
            var ex = new ParserException(message, GetLine(index), GetColumn(index), index);
            throw ex;
        }

        private Hashtable ParseObject(ref bool success)
        {
            var table                      = new Hashtable();
            var done                       = false;
            TOKENS token;

            // {
            NextToken(_charArray, ref _index, this._supportStartComment);

            var tokenAHead = LookAhead(_charArray, _index, this._supportStartComment);
            // Check for [{]
            // Check for {,
            if (tokenAHead.In(TOKENS.SQUARED_CLOSE, TOKENS.COMA, TOKENS.COLON))
            {
                NextToken(_charArray, ref _index, this._supportStartComment);
                this.ThrowError(SYNTAX_ERROR_014.format(TOKEN_STRING[tokenAHead]),  _index);
            }

            while (!done)
            {
                token = LookAhead(_charArray, _index, this._supportStartComment);
                 
                if (token == TOKENS.NONE)
                {
                    this.ThrowError(SYNTAX_ERROR_011, this._index);
                }
                else if (token == TOKENS.COMA)
                {
                    NextToken(_charArray, ref _index, this._supportStartComment);
                    if (LookAhead(_charArray, _index, this._supportStartComment).In(TOKENS.CURLY_CLOSE))
                    {
                        if (!this._supportTrailingComa)
                            this.ThrowError(SYNTAX_ERROR_001, this._index);
                    }
                }
                else if (token == TOKENS.CURLY_CLOSE)
                {
                    NextToken(_charArray, ref _index, this._supportStartComment);
                    return table;
                }
                else
                {
                    // name with or without id
                    string name = "";

                    if (token == TOKENS.ID && this._supportIDWithNoQuote)
                    {
                        name = ParseID(this._charArray, ref _index, ref success, this._supportStartComment).ToString();
                    }
                    else if (token == TOKENS.STRING)
                    {
                        var s = ParseString(ref _index, ref success, this._supportStartComment);
                        if (s == null)
                        {                
                            this.ThrowError(SYNTAX_ERROR_017, this._index);
                        }
                        else
                        {
                            name = s.ToString();
                        }
                    }
                    else
                    {
                        success = false;
                    }

                    if (!success)
                    {
                        var tmpTok = NextToken(_charArray, ref _index, this._supportStartComment);
                        this.ThrowError(SYNTAX_ERROR_007, this._index);
                    }

                    // :
                    token = NextToken(_charArray, ref _index, this._supportStartComment);
                    if (token != TOKENS.COLON)
                    {
                        this.ThrowError(SYNTAX_ERROR_004, this._index);
                    }

                    // value
                    object value = ParseValue(ref success);
                    if (!success)
                    {
                        this.ThrowError(SYNTAX_ERROR_008, this._index);
                    }

                    
                    var next2Tokens = GetNext2TokensAHead();
                    if (next2Tokens.Count >= 1 && (!next2Tokens[0].In(TOKENS.COMA, TOKENS.CURLY_CLOSE)))
                    {
                        this.ThrowError(SYNTAX_ERROR_010, this._index);
                    } // Check for ,, after an value
                    else if (next2Tokens.Count == 2 && next2Tokens[0] == TOKENS.COMA && next2Tokens[1] == TOKENS.COMA)
                    {
                        this.ThrowError(SYNTAX_ERROR_012, this._index);
                    }
                    else if (next2Tokens.Count == 2 && next2Tokens[0] == TOKENS.COMA && next2Tokens[1] == TOKENS.SQUARED_CLOSE)
                    {
                        if (!this._supportTrailingComa)
                            this.ThrowError(SYNTAX_ERROR_001, this._index);
                    }
                    else if (next2Tokens.Count == 2 && next2Tokens[0] == TOKENS.NONE && next2Tokens[1] == TOKENS.NONE)
                    {   // If we are missing a } but are at the eof
                        this.ThrowError(SYNTAX_ERROR_003, this._index);
                    }

                    table[name] = value;
                }
            }
            return table;
        }

        private List<TOKENS> GetNext2TokensAHead()
        {
            var l = new List<TOKENS>();
            int index2 = _index;
            l.Add(NextToken(this._charArray, ref _index, this._supportStartComment));
            l.Add(NextToken(_charArray, ref _index, this._supportStartComment));
            _index = index2;
            return l;
        }

        private ArrayList ParseArray(ref bool success)
        {
            var array = new ArrayList();
           
            // [
            NextToken(_charArray, ref _index, this._supportStartComment);

            bool done = false;

            var tokenAHead = LookAhead(_charArray, _index, this._supportStartComment);

            // Check for [,
            // Check for [}
            if (tokenAHead.In(TOKENS.COMA, TOKENS.CURLY_CLOSE))
            {
                NextToken(_charArray, ref _index, this._supportStartComment);
                this.ThrowError(SYNTAX_ERROR_014.format(TOKEN_STRING[tokenAHead]), this._index);
            }

            while (!done)
            {
                var token = LookAhead(_charArray, _index, this._supportStartComment);
  

                if (token == TOKENS.NONE)
                {
                    this.ThrowError(SYNTAX_ERROR_002, this._index);
                }
                else if (token == TOKENS.COMA)
                {
                    NextToken(_charArray, ref _index, this._supportStartComment);
                    if (LookAhead(_charArray, _index, this._supportStartComment).In(TOKENS.CURLY_CLOSE))
                    {
                        if (!this._supportTrailingComa)
                            this.ThrowError(SYNTAX_ERROR_001, this._index);
                    }
                }
                else if (token == TOKENS.SQUARED_CLOSE)
                {
                    NextToken(_charArray, ref _index, this._supportStartComment);
                    break;
                }
                else
                {
                    object value = ParseValue(ref success);
                    if (!success)
                    {
                        this.ThrowError(SYNTAX_ERROR_008, this._index);
                    }
                    array.Add(value);

                    var nextToken = LookAhead(_charArray, _index, this._supportStartComment);
                    if (nextToken != TOKENS.SQUARED_CLOSE && nextToken != TOKENS.COMA)
                    {
                        this.ThrowError(SYNTAX_ERROR_009, this._index);
                    }

                    // Check for ,, after an value
                    var next2Tokens = GetNext2TokensAHead();
                    if (next2Tokens.Count == 2 && next2Tokens[0] == TOKENS.COMA && next2Tokens[1] == TOKENS.COMA)
                    {
                        this.ThrowError(SYNTAX_ERROR_012, this._index);
                    }
                    if (next2Tokens.Count == 2 && next2Tokens[0] == TOKENS.COMA && next2Tokens[1] == TOKENS.SQUARED_CLOSE)
                    {
                        if (!this._supportTrailingComa)
                            this.ThrowError(SYNTAX_ERROR_001, this._index);
                    }
                }
            }
            return array;
        }

        private object ParseValue(ref bool success)
        {
            var token = LookAhead(_charArray, _index, this._supportStartComment);
            switch (token)
            {
                case TOKENS.STRING:
                    return ParseString(ref _index, ref success, this._supportStartComment);
                case TOKENS.NUMBER:
                    return ParseNumber(this._charArray, ref _index, ref success, this._supportStartComment);
                case TOKENS.CURLY_OPEN:
                    return ParseObject(ref success);
                case TOKENS.SQUARED_OPEN:
                    return ParseArray(ref success);
                case TOKENS.TRUE:
                    NextToken(this._charArray, ref _index, this._supportStartComment);
                    return true;
                case TOKENS.FALSE:
                    NextToken(this._charArray, ref _index, this._supportStartComment);
                    return false;
                case TOKENS.NULL:
                    NextToken(this._charArray, ref _index, this._supportStartComment);
                    return null;
                case TOKENS.NONE:
                    break;
            }
            this.ThrowError(SYNTAX_ERROR_014.format(TOKEN_STRING[token]), this._index);
            return null; // will never be executed
        }

        private object ParseString(ref int index, ref bool success, bool supportStartComment)
        {
            var indexSaved = index;
            StringBuilder s = new StringBuilder(BUILDER_CAPACITY);
            char c;

            EatWhitespace(_charArray, ref index, supportStartComment);

            c = _charArray[index++];

            bool complete = false;
            while (!complete)
            {
                if (index == _charArray.Length)
                {
                    break;
                }

                c = _charArray[index++];
                if (c == '"')
                {
                    complete = true;
                    break;
                }
                else if (c == '\\')
                {
                    if (index == _charArray.Length)
                    {
                        break;
                    }
                    c = _charArray[index++];
                    if (c == '"')
                    {
                        s.Append('"');
                    }
                    else if (c == '\\')
                    {
                        s.Append('\\');
                    }
                    else if (c == '/')
                    {
                        s.Append('/');
                    }
                    else if (c == 'b')
                    {
                        s.Append('\b');
                    }
                    else if (c == 'f')
                    {
                        s.Append('\f');
                    }
                    else if (c == 'n')
                    {
                        s.Append('\n');
                    }
                    else if (c == 'r')
                    {
                        s.Append('\r');
                    }
                    else if (c == 't')
                    {
                        s.Append('\t');
                    }
                    else if (c == 'u')
                    {
                        int remainingLength = _charArray.Length - index;
                        if (remainingLength >= 4)
                        {
                            // parse the 32 bit hex into an integer codepoint
                            uint codePoint;
                            if (!(success = UInt32.TryParse(new string(_charArray, index, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out codePoint)))
                            {
                                return "";
                            }
                            // convert the integer codepoint to a unicode char and add to string
                            s.Append(Char.ConvertFromUtf32((int)codePoint));
                            // skip 4 chars
                            index += 4;
                        }
                        else
                        {
                            break;
                        }
                    }

                }
                else
                {
                    s.Append(c);
                }
            }

            if (!complete)
            {
                index   = indexSaved; // Restore the position where we started parsing the string, this give a better error message
                success = false;
                return null;
            }

            var val = s.ToString();
            if (IsJsonDate(val))
                return ParseJsonDateTime(val);
            else
                return val;
        }
    }
}

