using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class RingBufferTest
{
    // A Test behaves as an ordinary method
    [Test]
    public void Tests_NewTestScriptSimplePasses()
    {
        RingBuffer<int> ringBuffer = new RingBuffer<int>(10);

    }
}
