using System;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using CsQuery;
using Sitecore.Diagnostics;
using SitecoreCDN.Util;

namespace SitecoreCDN.Filters
{

    /// <summary>
    /// A filter stream that allows the replacing of img/script src attributes (and link tag's href attribute) 
    /// with CDN appended urls
    /// 
    /// i.e.   "~/media/path/to/file.ashx?w=400&h=200"  becomes "http://mycdnhostname/~/media/path/to/file.ashx?w=400&h=200&v=2&d=20130101T000000"
    /// 
    /// </summary>
    public class MediaUrlFilter : Stream
    {
        private Stream _responseStream;
        private long _position;
        private StringBuilder _sb;
        private bool _isComplete;

        public MediaUrlFilter(Stream inputStream)
        {
            _responseStream = inputStream;
            _sb = new StringBuilder();
            _isComplete = false;
        }


        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override void Flush()
        {
            // if the stream wasn't completed by Write, output the contents of the inner stream first
            if (!_isComplete)
            {
                byte[] data = UTF8Encoding.UTF8.GetBytes(_sb.ToString());
                _responseStream.Write(data, 0, data.Length);
            }
            _responseStream.Flush();
        }

        public override void Close()
        {
            _responseStream.Close();
        }

        public override long Length
        {
            get { return 0; }
        }

        public override long Position
        {
            get { return _position; }
            set { _position = value; }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return _responseStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _responseStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _responseStream.SetLength(value);
        }


        /// <summary>
        /// This Method buffers the original Write payloads until the end of the end [/html] tag
        /// when replacement occurs
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            // preview the contents of the payload
            string content = UTF8Encoding.UTF8.GetString(buffer, offset, count);

            Regex eof = new Regex("</html>", RegexOptions.IgnoreCase);
            // if the content contains </html> we know we're at the end of the line
            // otherwise append the contents to the stringbuilder
            if (!eof.IsMatch(content))
            {
                if (_isComplete)
                {
                    _responseStream.Write(buffer, offset, count);
                }
                else
                {
                    _sb.Append(content);
                }
            }
            else
            {
                _sb.Append(content.Substring(0, content.IndexOf("</html>") + 7));


                try
                {
                    using (new TimerReport("replaceMediaUrls"))
                    {


                        CQ dom = CQ.Create(_sb.ToString(),
                            HtmlParsingMode.Auto,
                            HtmlParsingOptions.Default,
                            DocType.HTML5);


                        // replace appropriate urls
                        CDNManager.ReplaceMediaUrls(dom);


                        string html =
                            dom.Render(OutputFormatters.HtmlEncodingNone);


                        UTF8Encoding encoding = new UTF8Encoding(true);

                        StreamWriter sw = new StreamWriter(_responseStream, encoding);

                        sw.Write(html);
                        sw.Flush();
                    }
                    _isComplete = true;
                }
                catch (Exception ex)
                {
                    Log.Error("CDN MediaURL Filter Error", ex, this);
                }
            }
        }
    }
}
