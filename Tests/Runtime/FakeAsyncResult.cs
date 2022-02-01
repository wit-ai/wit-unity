using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
namespace Facebook.WitAi
{


    public class FakeAsyncResult : IAsyncResult
    {
        public object AsyncState
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public WaitHandle AsyncWaitHandle
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public bool CompletedSynchronously
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public bool IsCompleted
        {
            get
            {
                throw new NotImplementedException();
            }
        }
    }

}
