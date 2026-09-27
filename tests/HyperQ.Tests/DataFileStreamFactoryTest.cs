using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Reflection;
using HyperQ.Util;

namespace HyperQ.Test
{
    [TestClass]
    public class DataFileStreamFactoryTest
    {
        private static string MakeTempPath()
        {
            string baseDir = Path.Combine(Path.GetTempPath(), "DataStreamFactoryTest" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(baseDir);
            return Path.Combine(baseDir, "map.dat");
        }

        [TestMethod]
        public void FactoryReturnsSameStreamForSamePath()
        {
            string baseDir = Path.Combine(Path.GetTempPath(), "DataStreamFactoryTest" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(baseDir);
            string basePath = Path.Combine(baseDir, "map");

            FileIndexMapper<int> m1 = new FileIndexMapper<int>(basePath);
            FileIndexMapper<int> m2 = new FileIndexMapper<int>(basePath);

            FieldInfo fi = typeof(FileIndexMapper<int>).GetField("_dataStream", BindingFlags.NonPublic | BindingFlags.Instance);
            var fs1 = fi.GetValue(m1);
            var fs2 = fi.GetValue(m2);

            Assert.AreSame(fs1, fs2, "Factory did not return the same FileStream instance");
        }

        [TestMethod]
        public void GetReturnsSameStreamWhileAlive()
        {
            string path = MakeTempPath();

            FileStream s1 = DataFileStreamFactory.Get(path);
            FileStream s2 = DataFileStreamFactory.Get(path);

            Assert.AreSame(s1, s2, "Factory did not reuse the live stream");
        }

        [TestMethod]
        public void GetRecreatesStreamAfterConsumerDisposesIt()
        {
            string path = MakeTempPath();

            FileStream first = DataFileStreamFactory.Get(path);
            first.Dispose();

            FileStream second = DataFileStreamFactory.Get(path);
            Assert.AreNotSame(first, second, "Factory returned the disposed stream from the cache");
            Assert.IsTrue(second.CanWrite, "Re-created stream is not writable");
            second.Seek(0, SeekOrigin.Begin);
            second.WriteByte(42);
            second.Flush(true);
        }

        [TestMethod]
        public void DisposeAllDisposesCreatedStreams()
        {
            string path = MakeTempPath();

            FileStream s1 = DataFileStreamFactory.Get(path);
            DataFileStreamFactory.DisposeAll();

            Assert.ThrowsException<ObjectDisposedException>(() => s1.WriteByte(1), "Stream was not disposed by DisposeAll");
        }

        [TestMethod]
        public void GetRejectsInvalidArguments()
        {
            Assert.ThrowsException<ArgumentException>(() => DataFileStreamFactory.Get(null));
            Assert.ThrowsException<ArgumentException>(() => DataFileStreamFactory.Get(string.Empty));
            Assert.ThrowsException<ArgumentException>(() => DataFileStreamFactory.Get("   "));
        }
    }
}
