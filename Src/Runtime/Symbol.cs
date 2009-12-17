/*
 Copyright 2009 David Piepgrass

 Permission is hereby granted, free of charge, to any person
 obtaining a copy of this software and associated documentation
 files (the "Software"), to deal in the Software without
 restriction, including without limitation the rights to use,
 copy, modify, merge, publish, distribute, sublicense, and/or sell
 copies of the Software, and to permit persons to whom the
 Software is furnished to do so, subject to the following
 conditions:

 The above copyright notice and this permission notice shall be
 included in all copies or substantial portions of the Software's
 source code.

 THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
 EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
 OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
 NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
 HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
 WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
 OTHER DEALINGS IN THE SOFTWARE.

 The above license applies to Symbol.cs only. Most of Loyc uses the LGPL license.
*/
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace Loyc.Runtime
{
	/// <summary>Represents a symbol, like the feature offered in Ruby.</summary>
	/// <remarks>
	/// Call Symbol.Get() to create a Symbol from a string, or Symbol.GetIfExists()
	/// to retrieve a Symbol that has already been created.
	/// <para/>
	/// Symbols are used like a global, extensible enumeration. Comparing symbols is
	/// as fast as comparing two integers; this is because '==' is not
	/// overloaded--equality is defined as reference equality, as there is only one
	/// instance of a given Symbol.
	/// <para/>
	/// A Symbol's ToString() function returns the symbol name prefixed with a colon
	/// (:), following the convention of the Ruby language, from which I got the
	/// idea of Symbols in the first place. The Name property returns the original
	/// string without the colon.
	/// <para/>
	/// Note: Symbol can represent any string, not just identifiers.
	/// </remarks>
	public class Symbol
	{
		#region Public static members

		static public Symbol Get(string name) { return PublicPool.Get(name); }
		static public Symbol GetIfExists(string name) { return PublicPool.GetIfExists(name); }
		static public Symbol GetById(int id) { return PublicPool.GetById(id); }

		static public readonly Symbol Empty;
		static public readonly SymbolPool PublicPool = SymbolPool.PublicPool;

		#endregion

		#region Public instance members

		public int Id          { [DebuggerStepThrough] get { return _id; } }
		public string Name     { [DebuggerStepThrough] get { return _name; } }
		public SymbolPool Pool { [DebuggerStepThrough] get { return _pool; } }
		public bool IsPublic   { [DebuggerStepThrough] get { return _pool == PublicPool; } }
		
		[DebuggerStepThrough]
		public override string ToString()
		{
			if (_id == 0)
				return string.Empty;
			else
				return ":" + Name;
		}

		public override int GetHashCode() { return _id ^ (_pool.PoolId << 6); }
		public override bool Equals(object b) { return ReferenceEquals(this, b); }
		
		#endregion

		#region Protected & private members

		private readonly int _id;
		private readonly string _name;
		private readonly SymbolPool _pool;
		
		/// <summary>For internal use only. Call Symbol.Get() instead!</summary>
		internal Symbol(int id, string name, SymbolPool pool) 
			{ _id = id; _name = name; _pool = pool; }
		
		/// <summary>For use by a derived class to produce a statically-typed 
		/// enumeration in a private pool. See the example under SymbolPool 
		/// (of SymbolEnum)</summary>
		/// <param name="prototype">A strictly temporary Symbol that is used
		/// to initialize this object. The derived class should discard the
		/// prototype after calling this constructor.</param>
		protected Symbol(Symbol prototype)
		{
			_id = prototype._id;
			_name = prototype._name;
			_pool = prototype._pool;
		}

		static Symbol()
		{
			Empty = new Symbol(0, string.Empty, PublicPool);
			PublicPool._list.Add(Empty);
			PublicPool._map[string.Empty] = Empty;
		}

		#endregion
	}

	/// <summary>Tracks a set of symbols.</summary>
	/// <remarks>
	/// There is one public symbol pool (Symbol.PublicPool) and you can create an 
	/// unlimited number of private pools, each with an independent namespace. 
	/// <para/>
	/// Methods of this class are synchronized, so a SymbolPool can be used from
	/// multiple threads.
	/// <para/>
	/// Symbols can be allocated, but they cannot be garbage-collected until the 
	/// pool in which the symbols were created is garbage-collected. Therefore, one 
	/// should avoid creating public Symbols based on user input, except in a short-
	/// running program. It is safer to create such symbols in a private pool, and 
	/// to free the pool when it is no longer needed.
	/// <para/>
	/// Symbols from private pools have negative IDs (normally starting at -1 and 
	/// proceeding down), and two private pools always produce duplicate IDs even 
	/// though Symbols in the two pools compare unequal. Symbols from the public 
	/// pool have non-negative IDs. Symbol.Empty has an ID of 0. Get("") returns 
	/// Symbol.Empty from the public pool, but in a private pool a new ID will be 
	/// allocated for ""; it is not treated differently than any other name.
	/// </remarks>
	public class SymbolPool : IEnumerable<Symbol>
	{
		static public readonly SymbolPool PublicPool = new SymbolPool(0, 0);

		protected internal List<Symbol> _list;
		protected internal Dictionary<string, Symbol> _map;
		protected internal readonly int _firstId;
		protected readonly int _poolId;
		protected static int _nextPoolId = 1;

		public SymbolPool() : this(-1, _nextPoolId++) { }
		
		/// <summary>Initializes a new Symbol pool.</summary>
		/// <param name="firstID">The first Symbol created in the pool will have 
		/// the specified ID, and IDs will proceed downward from there.</param>
		public SymbolPool(int firstID) : this(firstID, _nextPoolId++) { }

		protected internal SymbolPool(int firstID, int poolId)
		{
			_map = new Dictionary<string, Symbol>();
			_list = new List<Symbol>();
			_firstId = firstID;
			_poolId = poolId;
		}

		/// <summary>Gets a symbol from this pool, or creates it if it does not 
		/// exist in this pool already.</summary>
		/// <param name="name">Name to find or create.</param>
		/// <returns>A symbol with the requested name, or null if the name was null.</returns>
		/// <remarks>
		/// If Get("foo") is called in two different pools, two Symbols will be 
		/// created, each with the Name "foo" but not necessarily with the same 
		/// IDs. Note that two private pools re-use the same IDs, but this 
		/// generally doesn't matter, as Symbols are compared by reference, not by 
		/// ID.
		/// </remarks>
		public Symbol Get(string name)
		{
			Symbol result;
			Get(name, out result);
			return result;
		}
		
		/// <summary>Workaround for lack of covariant return types in C#</summary>
		protected virtual void Get(string name, out Symbol sym)
		{
			if (name == null)
				sym = null;
			else lock (_map)
				{
					if (!_map.TryGetValue(name, out sym))
					{
						if (this == PublicPool)
							name = string.Intern(name);
						int id = _list.Count;
						if (this == PublicPool)
							sym = new Symbol(id, name, this);
						else {
							id = _firstId - id;
							sym = NewSymbol(id, name, this);
						}
						_list.Add(sym);
						_map.Add(name, sym);
					}
				}
		}
		
		/// <summary>Factory method to create a new Symbol.</summary>
		protected virtual Symbol NewSymbol(int id, string name, SymbolPool pool)
		{
			return new Symbol(id, name, pool);
		}

		/// <summary>Gets a symbol from this pool, if the name exists already.</summary>
		/// <param name="name">Symbol Name to find</param>
		/// <returns>Returns the existing Symbol if found; returns null if the name 
		/// was not found, or if the name itself was null.</returns>
		public Symbol GetIfExists(string name)
		{
			Symbol sym;
			if (name == null)
				return null;
			else lock (_map)
				{
					_map.TryGetValue(name, out sym);
					return sym;
				}
		}
		
		/// <summary>Gets a symbol from the public pool, if it exists there already;
		/// otherwise, creates a Symbol in this pool.</summary>
		/// <param name="name">Name of a symbol to get or create</param>
		/// <returns>A symbol with the requested name</returns>
		public Symbol GetPublicOrCreateHere(string name)
		{
			Symbol sym = Symbol.PublicPool.GetIfExists(name);
			return sym ?? Get(name);
		}

		/// <summary>Gets a symbol by its ID, or null if there is no such symbol.</summary>
		/// <param name="id">ID of a symbol. If this is a private pool and the 
		/// ID does not exist in the pool, the public pool is searched instead.
		/// </param>
		/// <returns>The requested Symbol</returns>
		/// <exception cref="ArgumentException">The specified ID does not exist 
		/// in this pool or in the public pool.</exception>
		public Symbol GetById(int id)
		{
			int index = _firstId - id;
			if (this == PublicPool || unchecked((uint)index >= (uint)TotalCount)) {
				lock(PublicPool._map) {
					if (id < PublicPool._list.Count)
						return PublicPool._list[id];
				}
			} else {
				lock(_map) {
					return _list[index];
				}
			}
			throw new ArgumentException("Invalid Symbol ID " + id.ToString(), "id");
		}

		/// <summary>Returns the number of Symbols created in this pool.</summary>
		public int TotalCount
		{ 
			get { return _list.Count; }
		}

		protected internal int PoolId
		{
			get { return _poolId; }
		}
	
		#region IEnumerable<Symbol> Members

		public IEnumerator<Symbol> GetEnumerator()
		{
			return _list.GetEnumerator();
		}
		System.Collections.IEnumerator  System.Collections.IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		#endregion
	}

	/// <summary>This type of SymbolPool helps create more strongly typed Symbols
	/// that simulate enums, but provide extensibility. Specifically, it 
	/// creates SymbolE objects, where SymbolE is a derived class of Symbol.
	/// </summary>
	/// <typeparam name="SymbolE">
	/// A derived class of Symbol that owns the pool. See the example below.
	/// </typeparam>
	/// <example>
	/// public class ShapeType : Symbol
	/// {
	///     private ShapeType(Symbol prototype) : base(prototype) { }
	///     public static new readonly SymbolPool<ShapeType> Pool 
	///                          = new SymbolPool<ShapeType>(p => new ShapeType(p));
	///
	///     public static readonly ShapeType Circle  = Pool.Get("Circle");
	///     public static readonly ShapeType Rect    = Pool.Get("Rect");
	///     public static readonly ShapeType Line    = Pool.Get("Line");
	///     public static readonly ShapeType Polygon = Pool.Get("Polygon");
	/// }
	/// </example>
	public class SymbolPool<SymbolE> : SymbolPool, IEnumerable<SymbolE>
		where SymbolE : Symbol
	{
		public delegate SymbolE SymbolFactory(Symbol prototype);
		protected SymbolFactory _factory;
		
		public SymbolPool(SymbolFactory factory)
		{
			_factory = factory;
		}
		public SymbolPool(SymbolFactory factory, int firstID) : base(firstID)
		{
			_factory = factory;
		}
		public new SymbolE Get(string name)
		{
			return (SymbolE)base.Get(name);
		}
		protected override Symbol NewSymbol(int id, string name, SymbolPool pool)
		{
 			return _factory(new Symbol(id, name, pool));
		}
		public SymbolE GetIfExists(string name)
		{
			return (SymbolE)base.GetIfExists(name);
		}
		public SymbolE GetById(int id)
		{
			return (SymbolE)base.GetById(id);
		}
		
		#region IEnumerable<Symbol> Members

		public IEnumerator<SymbolE> GetEnumerator()
		{
			foreach (SymbolE symbol in _list)
				yield return symbol;
		}
		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}

		#endregion
	}

	[TestFixture]
	public class SymbolTests
	{
		[Test]
		public void BasicChecks()
		{
			Assert.AreEqual(null, Symbol.Get(null));
			Assert.AreEqual(0, Symbol.Get("").Id);
			Assert.AreEqual(Symbol.Empty, Symbol.GetById(0));

			Symbol foo = Symbol.Get("Foo");
			Symbol bar = Symbol.Get("Bar");
			Assert.AreNotEqual(foo, bar);
			Assert.AreEqual(":Foo", foo.ToString());
			Assert.AreEqual(":Bar", bar.ToString());
			Assert.AreEqual("Foo", foo.Name);
			Assert.AreEqual("Bar", bar.Name);
			Assert.IsNotNull(string.IsInterned(foo.Name));
			Assert.IsNotNull(string.IsInterned(bar.Name));

			Symbol foo2 = Symbol.Get("Foo");
			Symbol bar2 = Symbol.Get("Bar");
			Assert.AreNotEqual(foo2, bar2);
			Assert.AreEqual(foo, foo2);
			Assert.AreEqual(bar, bar2);
			Assert.That(object.ReferenceEquals(foo.Name, foo2.Name));
			Assert.That(object.ReferenceEquals(bar.Name, bar2.Name));
		}

		[Test]
		public void TestPrivatePools()
		{
			SymbolPool p1 = new SymbolPool();
			SymbolPool p2 = new SymbolPool(-3);
			SymbolPool p3 = new SymbolPool(0);
			Symbol a = Symbol.Get("a");
			Symbol b = Symbol.Get("b");
			Symbol c = Symbol.Get("c");
			Symbol s1a = p1.Get("a");
			Symbol s1b = p1.Get("b");
			Symbol s1c = p1.Get("c");
			Symbol s2a = p2.Get("a");
			Symbol s3a = p3.Get("a");
			Symbol s3b = p3.Get("b");

			Assert.That(s1a.Id == -1 && p1.GetById(-1) == s1a);
			Assert.That(s1b.Id == -2 && p1.GetById(-2) == s1b);
			Assert.That(s1c.Id == -3 && p1.GetById(-3) == s1c);
			Assert.That(s2a.Id == -3 && p2.GetById(-3) == s2a);
			Assert.That(s3b.Id == -1  && p3.GetById(-1) == s3b);
			Assert.That(s3a.Id == 0  && p3.GetById(0)  == s3a);
			Assert.AreEqual(Symbol.Empty, p1.GetById(0));
			Assert.AreEqual(s1c, p1.GetIfExists("c"));
			Assert.AreEqual(3, p1.TotalCount);
			Assert.AreEqual(null, p2.GetIfExists("c"));
			Assert.AreEqual(c, p2.GetPublicOrCreateHere("c"));
			Assert.AreEqual(p2, p2.GetPublicOrCreateHere("$!unique^&*").Pool);
		}

		public class ShapeType : Symbol
		{
			private ShapeType(Symbol prototype) : base(prototype) { }
			public static new readonly SymbolPool<ShapeType> Pool 
			                     = new SymbolPool<ShapeType>(delegate(Symbol p) { return new ShapeType(p); });

			public static readonly ShapeType Circle  = Pool.Get("Circle");
			public static readonly ShapeType Rect    = Pool.Get("Rect");
			public static readonly ShapeType Line    = Pool.Get("Line");
			public static readonly ShapeType Polygon = Pool.Get("Polygon");
		}

		[Test]
		public void TestDerivedSymbol()
		{
			int count = 0;
			foreach (ShapeType s in ShapeType.Pool) {
				count++;
				Assert.That(s == ShapeType.Circle || s == ShapeType.Rect ||
					       s == ShapeType.Polygon || s == ShapeType.Line);
				Assert.That(s.Id < 0);
				Assert.That(!s.IsPublic);
				Assert.AreEqual(s, ShapeType.Pool.GetById(s.Id));
				Assert.AreEqual(s, ShapeType.Pool.GetIfExists(s.Name));
			}
			Assert.AreEqual(4, count);
		}

		[Test]
		public void Exception()
		{
			try {
				Symbol.GetById(6543210);
				Symbol.GetById(-6543210);
				Assert.Fail("Expected ArgumentException");
			} catch (ArgumentException) { }
		}
	}
}

