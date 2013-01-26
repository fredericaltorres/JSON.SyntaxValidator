﻿/*
 JSON Syntax Validator
 (C) Torres Frederic 2013

 Based on code from: How do I write my own parser? (for JSON) By Patrick van Bergen http://techblog.procurios.nl/k/618/news/view/14605/14863/How-do-I-write-my-own-parser-for-JSON.html
 My library heavily change Patrick van Bergen's code. 
 
 The library is release under the Mit Style License

 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace JSON.SyntaxValidator
{
    public class CommentInfo {

        public int Start; 
        public int End;
        public int Length;
        public string Text;
        public bool Error;

        public bool IsPositionInComment(int pos)
        {
            return pos >= this.Start && pos <= this.End;
        }
    }
    public class CommentInfos : List<CommentInfo>
    {
        public string Hash;

        internal void UpdateHash()
        {
            var b = new StringBuilder(1000);
            foreach(var s in this)
            {
                b.Append(s.Text);
            }

            // step 1, calculate MD5 hash from input
            MD5 md5 = System.Security.Cryptography.MD5.Create();
            byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(b.ToString());
            byte[] hash = md5.ComputeHash(inputBytes);

            // step 2, convert byte array to hex string
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("X2"));
            }
            this.Hash = sb.ToString();
        }
        public bool IsPositionInComment(int pos)
        {
            foreach (var c in this)
            {
                if (c.IsPositionInComment(pos))
                {
                    return true;
                }
            }
            return false;
        }
    }
    /// <summary>
    /// Analyse the position of /* */ comment for color coding support
    /// </summary>
    public class CommentParser
    {
        public CommentParser()
        {
        }
        public CommentInfos Parse(string source, string startComment = "/*", string endComment = "*/", int numberOfCommentToParse = -1)
        {
            var l = new CommentInfos();
            int currentPos = 0;

            while (true)
            {
                var startPos = source.IndexOf(startComment, currentPos);
                if (startPos == -1)
                    break;
                else
                {
                    var ci     = new CommentInfo();
                    var endPos = source.IndexOf(endComment, startPos);
                    if (endPos == -1)
                    {
                        ci.Error = true;
                        break;
                    }
                    ci.Start = startPos;
                    ci.End = endPos + endComment.Length - 1;
                    ci.Length = endPos - startPos - startComment.Length;
                    ci.Text = source.Substring(startPos + startComment.Length, ci.Length);
                    l.Add(ci);
                    currentPos  = endPos+2;

                    if (numberOfCommentToParse != -1 && l.Count > numberOfCommentToParse)
                    {
                        break;
                    }
                }
            }
            l.UpdateHash();
            return l;
        }
    }
}
