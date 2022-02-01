using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using UnityEngine;
namespace Facebook.WitAi
{
    public class FakeRequest : IRequest
    {
        public WebHeaderCollection Headers { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string Method { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string ContentType { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public long ContentLength { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public bool SendChunked { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public string UserAgent { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public int Timeout { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public void Abort()
        {            
        }

        public IAsyncResult BeginGetRequestStream(AsyncCallback callback, object state)
        {
            return new FakeAsyncResult();            
        }

        public IAsyncResult BeginGetResponse(AsyncCallback callback, object state)
        {
            return new FakeAsyncResult();            
        }

        public Stream EndGetRequestStream(IAsyncResult asyncResult)
        {
            string s = "I am a test string.";
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        public WebResponse EndGetResponse(IAsyncResult asyncResult) // TODO: Replace 'WebResponse' with 'IResponse'.
        {
            throw new NotImplementedException();
        }
    }
}
