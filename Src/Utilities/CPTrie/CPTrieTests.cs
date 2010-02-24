﻿using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using System.Diagnostics;

namespace Loyc.Utilities
{
	[TestFixture]
	public class CPTrieTests
	{
		IEnumerable<string> BasicTestStrings()
		{
			string[] ss = new string[] {
				" --- THIS IS A TEST --- ",
				"",
				" ",
				"PREAMBLE:",
				"a Judish Patricia Trie",
				"contains, on occasion, long strings;",
				"if long enough, they could cause the JPLeaf to split",
				"it is interesting to note that several longish keys can fit in a JPLeaf",
				"so, I add several sets of strings to ensure that the leaves split.",
				"Noté thé Ŭnicodé.",
				@"C:\Windows",
				@"C:\Program Files",
				@"C:\Documents",				
				@"C:\Temp",
				@"C:\Temp\temp.txt",
				@"C:\Temp\temp.doc",
				"A",
				"An",
				"Ant",
				"Antler",
				"Apple",
 				"Bedtime",
				"Bed",
				"This",
				"is",
				"a",
				"test",
				"of",
				"the",
				"single",
				"word",
				"case",
				@"C:\AUTOEXEC.BAT",
				"z",
			};
			
			for (int set = 0; set < 16; set++)
			{
				string prefix = "";
				if (set > 0)
					prefix = (1 << set).ToString();

				for (int i = 0; i < ss.Length; i++)
					yield return prefix + ss[i];
			}
		}
		
		[Test]
		public void BasicTests()
		{
			CPStringTrie<string> trie = new CPStringTrie<string>();
			int count = 0;

			List<string> testStrings = new List<string>(BasicTestStrings());

			// Test insertion
			foreach (string key in testStrings)
			{
				trie.Add(key, key);
				Assert.AreEqual(++count, trie.Count);
			}
			
			Assert.That(trie.Contains(new KeyValuePair<string,string>("a", "a")));
			Assert.That(!trie.Contains(new KeyValuePair<string,string>("a", "")));

			// Test forward enumeration
			count = 0;
			string last = null;
			foreach (KeyValuePair<string, string> p in trie)
			{
				Assert.That(p.Key == p.Value);
				Assert.That(last == null || string.Compare(last, p.Value, StringComparison.Ordinal) < 0);
				last = p.Value;
				count++;
			}
			Assert.AreEqual(count, trie.Count);

			// Test deletion
			Debug.Assert(testStrings.Count % 16 == 0);
			for (int i = 0; i < testStrings.Count; i++)
			{
				// Remove keys in a different order than they were added
				string key = testStrings[i ^ 0xF];
				Assert.That(trie.Contains(new KeyValuePair<string,string>(key, key)));
				Assert.That(trie[key] == key);
				trie[key] = "!";
				Assert.That(trie[key] == "!");
				Assert.That(trie.Remove(key));
				Assert.That(!trie.ContainsKey(key));
			}
			Assert.AreEqual(0, trie.Count);
		}

		[Test]
		public void BasicEnumeratorTests()
		{
			CPStringTrie<string>.Enumerator e, e0;

			CPStringTrie<string> emptyTrie = new CPStringTrie<string>();
			Assert.IsNotNull(emptyTrie.FindAtLeast(""));
			Assert.That(!emptyTrie.FindAtLeast("foo").IsValid);
			Assert.IsNull(emptyTrie.FindExact(""));
			Assert.That(!emptyTrie.Find("?", out e) && e != null && !e.IsValid);

			CPStringTrie<string> trie = new CPStringTrie<string>();
			foreach (string key in BasicTestStrings())
				trie.Add(key, key);

			Assert.That(trie.Find("An", out e));
			Assert.That(!trie.Find("Am", out e0));
			Assert.AreEqual(e0.CurrentKey, e.CurrentValue);
			
			Assert.That(e.MovePrev());
			Assert.AreEqual("A", e.CurrentKey);
			Assert.AreEqual("A", e.CurrentValue);
			Assert.AreEqual("An", e0.CurrentKey);
			Assert.That(e0.MoveNext());
			Assert.AreEqual("Ant", e0.CurrentKey);
			
			Assert.That(!trie.Find("2Noté", out e));
			Assert.AreEqual("2Noté thé Ŭnicodé.", e.CurrentKey);
			Assert.IsNull(trie.FindExact("2Noté"));
			Assert.IsNotNull(trie.FindExact("2An"));
			Assert.AreEqual("16single", trie.FindAtLeast("16simgle").CurrentKey);
			
			Assert.That(!trie.Find("32zzz", out e));
			Assert.AreEqual("4", e.CurrentKey);
			Assert.That(e.MoveNext());
			Assert.AreEqual("4 ", e.CurrentKey);
			Assert.That(e.MovePrev());
			Assert.AreEqual("4", e.CurrentKey);
			Assert.That(e.MovePrev());
			Assert.AreEqual("32z", e.CurrentKey);
			
			Assert.That(!trie.Find("zzz", out e));
			Assert.That(!e.IsValid);
			Assert.That(e.MovePrev());
			Assert.AreEqual("z", e.CurrentValue);

			TestFind(trie, 500);
		}

		private void TestFind(CPStringTrie<string> trie, int iterations)
		{
			Random rand = new Random();
			CPStringTrie<string>.Enumerator e;

			// Search for random character pairs and make sure the returned
			// enumerator points to the right place.
			for (int i = 0; i < iterations; i++)
			{
				char c0 = (char)rand.Next((int)' ', (int)'z' + 1);
				char c1 = (char)rand.Next((int)' ', (int)'z' + 1);
				string s = c0.ToString() + c1.ToString();

				bool found = trie.Find(s, out e);
				string curKey = e.IsValid ? e.CurrentKey : null;

				bool havePrev = e.MovePrev();
				if (found)
				{
					Assert.AreEqual(s, curKey);
				}
				else if (curKey != null)
				{
					Assert.That(string.CompareOrdinal(s, curKey) < 0);
				}
				else
					Assert.That(havePrev);

				Assert.That(havePrev == e.IsValid);
				if (havePrev)
				{
					curKey = e.CurrentKey;
					Assert.That(string.CompareOrdinal(s, curKey) > 0);
				}
			}
		}

		[Test]
		public void DenseNodeTest()
		{
			CPByteTrie<object> trie = new CPByteTrie<object>();
			Dictionary<byte[], object> hash = new Dictionary<byte[], object>(new ByteArrayComparer());
			Random rand = new Random(0);
			byte[] suffix = { (byte)'s', (byte)'u', (byte)'f', (byte)'f', (byte)'i', (byte)'x' };
			
			// Construct a trie with dense nodes at various levels of the trie.
			for (int i = 0; i < 256; i++)
			{
				int suffixLen = rand.Next(suffix.Length + 1);
				byte[] key1 = new byte[2 + suffixLen];
				byte[] key2 = new byte[2 + suffixLen];
				byte[] key3 = new byte[1 + suffixLen];
				key1[0] = (byte)'A';
				key1[1] = (byte)i;
				key2[0] = (byte)'B';
				key2[suffixLen + 1] = (byte)i;
				key3[0] = (byte)i;
				for (int s = 0; s < suffixLen; s++) {
					key1[s + 2] = suffix[s];
					key2[s + 1] = suffix[s];
					key3[s + 1] = suffix[s];
				}
				if (trie.TryAdd(key1, key1))
					hash.Add(key1, key1);
				trie[key2] = null;
				trie[key3] = null;
				hash[key2] = null;
				hash[key3] = null;

				Assert.AreEqual(trie.Count, hash.Count);
			}

			// Test retrieval
			foreach (byte[] key in hash.Keys)
			{
				Assert.That(trie.ContainsKey(key));
				Assert.That(object.ReferenceEquals(trie[key], hash[key]));
			}

			// Test basic enumeration
			CPByteTrie<object>.Enumerator e = trie.GetEnumerator();
			byte[] prevKey = null, curKey;
			int count = 0;
			while (e.MoveNext())
			{
				curKey = e.CurrentKey;
				Assert.That(hash.ContainsKey(curKey));
				Assert.That(ByteArrayComparer.Compare(prevKey, curKey) < 0);

				if (prevKey != null && rand.Next(3) == 0)
				{
					// Enumerate backward
					Assert.That(e.MovePrev());
					Assert.That(ByteArrayComparer.Compare(prevKey, e.CurrentKey) == 0);

					// Find() our way back to the current item
					Assert.That(trie.Find(curKey, out e));
					Assert.That(ByteArrayComparer.Compare(curKey, e.CurrentKey) == 0);
				}
				
				prevKey = curKey;
				count++;
			}
			Assert.AreEqual(trie.Count, count);

			// Remove each item
			foreach (byte[] key in hash.Keys)
				Assert.That(trie.Remove(key));

			Assert.That(trie.IsEmpty);
		}
	}
	
	class ByteArrayComparer : IEqualityComparer<byte[]>
	{
		public bool Equals(byte[] x, byte[] y)
		{
			return Compare(x, y) == 0;
		}
		public int GetHashCode(byte[] obj)
		{
			int hc = obj.Length;
			for (int i = 0; i < obj.Length; i++)
				hc = hc * 257 ^ obj[i];
			return hc;
		}
		/// <summary>Compares two byte arrays, returning -1 if A is less than B and
		/// 1 if A is greater than B.</summary>
		/// <remarks>null is considered "less" than any non-null array.</remarks>
		public static int Compare(byte[] A, byte[] B)
		{
			if (A == null || B == null)
			{
				if (A != null) return 1;
				if (B != null) return -1;
				return 0;
			}
			for (int i = 0; i < A.Length; i++)
			{
				if (i == B.Length)
					return 1;
				if (A[i] != B[i])
					return A[i] < B[i] ? -1 : 1;
			}
			return A.Length == B.Length ? 0 : -1;
		}
	};
}