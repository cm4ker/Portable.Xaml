using System;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Linq;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using Microsoft.CodeAnalysis.Formatting;
using System.Reflection;

namespace Portable.Xaml.Roslyn
{
	public class XamlRoslynWriterSettings
	{
		public string Language;
		public SyntaxNode RootObjectSyntaxNode;
	}

	public class RoslynWriter : Portable.Xaml.XamlWriter
	{
		SyntaxGenerator _syntax;
		XamlSchemaContext _context;
		SyntaxNode _top;
		Workspace _workspace;

		SyntaxGenerator Syntax => _syntax;

		readonly XamlRoslynWriterInternal _intl;
		
		public RoslynWriter(XamlSchemaContext schemaContext, XamlRoslynWriterSettings settings, string language = LanguageNames.CSharp)
		{
			_workspace = new AdhocWorkspace();

			_syntax = SyntaxGenerator.GetGenerator(_workspace, language);
			_context = schemaContext;
			var manager = new XamlWriterStateManager<XamlObjectWriterException, XamlObjectWriterException>(false);
			_intl = new XamlRoslynWriterInternal(schemaContext, manager);
			
		}

		public string ToCode()
		{
			var method = Syntax.MethodDeclaration("InitializeComponent", statements: statements);
			_top = Syntax.AddMembers(_top, method);
			return Formatter.Format(_top, _workspace).ToFullString();
		}

		public override XamlSchemaContext SchemaContext => _context;

		public override void WriteEndMember()
		{
		}

		public override void WriteEndObject()
		{
			var node = CurrentNode;
			types.Pop();

			if (types.Count > 0)
			{
				var initializer = Syntax.ObjectCreationExpression(node.TypeName(this));
				if (!string.IsNullOrEmpty(node.ID))
				{
					initializer = Syntax.AssignmentStatement(Syntax.IdentifierName(node.ID), initializer);
				}

				statements.Add(Syntax.LocalDeclarationStatement(node.GetName(this), initializer));
			}

			if (node.HasStatements)
				statements.AddRange(node.Statements);

			var member = CurrentNode?.Member;
			if (member != null)
			{
				if (member.Type.IsCollection)
				{
					// get correct add method, or just call blah.Add()
				}
				else
				{
					statements.Add(Syntax.AssignmentStatement(
						Syntax.MemberAccessExpression(CurrentObject, Syntax.IdentifierName(member.Name)),
						Syntax.IdentifierName(node.GetName(this))));
				}
			}
		}

		public override void WriteGetObject()
		{
		}

		public override void WriteNamespace(NamespaceDeclaration namespaceDeclaration)
		{
		}

		class Node
		{
			public XamlType Type;
			public SyntaxNode Create;
			public XamlMember Member;
			public string ID;
			SyntaxNode _typeName;
			public SyntaxNode TypeName(RoslynWriter w) => _typeName ?? (_typeName = w.GetType(Type));
			public Action<object> SetValue;
			List<SyntaxNode> _statements;
			string _name;
			public bool HasStatements => _statements?.Count > 0;
			public List<SyntaxNode> Statements => _statements ?? (_statements = new List<SyntaxNode>());

			public string SetName(string name) => _name = name;

			public string GetName(RoslynWriter w)
			{
				if (_name != null)
					return _name;

				int count;
				if (!w.nameCount.TryGetValue(Type.Name, out count))
				{
					count = 0;
				}

				count++;
				w.nameCount[Type.Name] = count;
				return _name = "_" + Type.Name + count;
			}
		}

		Dictionary<string, int> nameCount = new Dictionary<string, int>();
		Stack<Node> types = new Stack<Node>();
		List<SyntaxNode> statements = new List<SyntaxNode>();
		SyntaxNode createObject;

		Node CurrentNode => types.Count > 0 ? types.Peek() : null;

		SyntaxNode GetType(XamlType type) => Syntax.DottedName(type.UnderlyingType.FullName);

		SyntaxNode CurrentObject =>
			types.Count == 1 ? Syntax.ThisExpression() : Syntax.IdentifierName(CurrentNode.GetName(this));

		public override void WriteStartMember(XamlMember xamlMember)
		{
			var node = CurrentNode;
			node.Member = xamlMember;
			if (xamlMember == XamlLanguage.Class)
			{
				node.SetValue = val => _top = Syntax.ClassDeclaration(val.ToString(),
					modifiers: DeclarationModifiers.Partial, baseType: node.TypeName(this));
				return;
			}

			if (xamlMember == XamlLanguage.Name)
			{
				// write as protected field
				node.SetValue = val =>
				{
					node.ID = val.ToString();
					_top = Syntax.AddMembers(_top, Syntax.FieldDeclaration(val.ToString(), node.TypeName(this)));
					var aliasedName = node.Type.GetAliasedProperty(XamlLanguage.Name);
					if (aliasedName != null)
					{
						node.Statements.Add(Syntax.AssignmentStatement(
							Syntax.MemberAccessExpression(CurrentObject, Syntax.IdentifierName(aliasedName.Name)),
							Syntax.LiteralExpression(val)));
					}
				};
				return;
			}

			if (!xamlMember.IsDirective)
			{
				node.SetValue = val =>
				{
					node.Statements.Add(Syntax.AssignmentStatement(
						Syntax.MemberAccessExpression(CurrentObject, Syntax.IdentifierName(xamlMember.Name)),
						Syntax.LiteralExpression(val)));
				};
			}
		}

		public override void WriteStartObject(XamlType type)
		{
			_intl.WriteStartObject(type);
			
			var node = new Node {Type = type};
			types.Push(node);
		}

		public override void WriteValue(object value)
		{
			// todo: translate value to the correct type here, or in SetValue?
			CurrentNode?.SetValue?.Invoke(value);
		}
	}


	class XamlRoslynWriterInternal : XamlWriterInternalBase
	{
		
		SyntaxGenerator _syntax;
		XamlSchemaContext _context;
		SyntaxNode _top;
		Workspace _workspace;
		
		List<SyntaxNode> statements = new List<SyntaxNode>();
		
		SyntaxGenerator Syntax => _syntax;
		
		public XamlRoslynWriterInternal(XamlSchemaContext schemaContext, XamlWriterStateManager manager) : base(
			schemaContext, manager)
		{
		}

		public XamlRoslynWriterInternal(XamlSchemaContext schemaContext, XamlWriterStateManager manager,
			IAmbientProvider parentAmbientProvider = null) : base(schemaContext, manager, parentAmbientProvider)
		{
		}

		public string ToCode()
		{
			
			_top = Syntax.ClassDeclaration(root_state.Value.ToString(),
				modifiers: DeclarationModifiers.Partial, baseType: Syntax.DottedName(root_state.Type.UnderlyingType.FullName));
			
			var method = Syntax.MethodDeclaration("InitializeComponent", statements: statements);
			_top = Syntax.AddMembers(_top, method);
			return Formatter.Format(_top, _workspace).ToFullString();
		}
		
		protected override void OnWriteEndObject()
		{
			throw new NotImplementedException();
		}

		protected override void OnWriteEndMember()
		{
			throw new NotImplementedException();
		}

		protected override void OnWriteStartObject()
		{
			var state = object_states.Pop();
			if (object_states.Count > 0)
			{
				
			}
			else
			{
				// this is root state
				root_state = state;
			}
			object_states.Push(state);
			state.IsXamlWriterCreated = true;
		}

		protected override void OnWriteGetObject()
		{
			throw new NotImplementedException();
		}

		protected override void OnWriteStartMember(XamlMember xm)
		{
			var state = object_states.Pop();
			if (object_states.Count > 1)
			{
				//this is need to be seated to the field name or smth else 
			}
			else
			{
				// this is root object we need create new field and set it to the this field
				root_state = state;
			}
			object_states.Push(state);
		}

		protected override void OnWriteValue(object value)
		{
			throw new NotImplementedException();
		}

		protected override void OnWriteNamespace(NamespaceDeclaration nd)
		{
			throw new NotImplementedException();
		}
		
		
			
		void InitializeObjectIfRequired (bool waitForParameters, bool required = false)
		{
			var state = object_states.Peek ();
			if (state.IsInstantiated)
				return;

			object obj = null;
			if ((state.Type.ConstructionRequiresArguments && !required)
			    || (waitForParameters && state.Type.HasPositionalParameters(service_provider)))
			{
				if (!state.Type.IsImmutable)
					return;
				
				obj = state.Type.Invoker.ToMutable(null);
				if (obj == null)
					return;
			}

			if (obj == null)
			{
				// FIXME: "The default techniques in absence of a factory method are to attempt to find a default constructor, then attempt to find an identified type converter on type, member, or destination type."
				// http://msdn.microsoft.com/en-us/library/System.Xaml.xamllanguage.factorymethod%28VS.100%29.aspx
				if (state.FactoryMethod != null) // FIXME: it must be implemented and verified with tests.
					throw new NotImplementedException();
				else
				{
					if (state.Type.ConstructionRequiresArguments)
					{
						var constructorProps = state.WrittenProperties.Where(r => r.Member.IsConstructorArgument).ToList();

						// immutable type (no default constructor), so we create based on supplied constructor arguments 
						var args = state.Type.GetSortedConstructorArguments(constructorProps)?.ToList();
						if (args == null)
							throw new XamlObjectWriterException($"Could not find constructor for {state.Type} based on supplied members");

						var argValues = args.Select(r => r.Value).ToArray();

						obj = state.Type.Invoker.CreateInstance(argValues);
						state.Value = obj;
						state.IsInstantiated = true;


						// set other writable properties now that the object is instantiated
						foreach (var prop in state.WrittenProperties.Where(p => args.All(r => r.Member != p.Member)))
						{
							if (prop.Member.IsReadOnly && prop.Member.IsConstructorArgument)
								throw new XamlObjectWriterException($"Member {prop.Member} is read only and cannot be used in any constructor");
							if (!prop.Member.IsReadOnly)
								SetValue(prop.Member, prop.Value);
						}
						return;
					}
					else
						obj = state.Type.Invoker.CreateInstance(null);
					
					if (state.Type.IsImmutable)
						obj = state.Type.Invoker.ToMutable(obj);
				}
			}

			state.Value = obj;
			state.IsInstantiated = true;
		}

		
		void SetValue(XamlMember member, object value)
		{
			if (ReferenceEquals(member, XamlLanguage.FactoryMethod))
				object_states.Peek().FactoryMethod = (string)value;
			else if (member.IsDirective)
				return;
			else
			{
				var state = object_states.Peek();
				// won't be instantiated yet if dealing with a type that has no default constructor
				if (state.IsInstantiated && !state.CurrentMemberState.IsAlreadySet 
				                         && !(state.IsValueProvidedByParent && state.CurrentMember.Type.IsCollection))
					SetValue(member, state.Value, value);
			}
		}

		void SetValue(XamlMember member, object target, object value)
		{
			try
			{
				member.Invoker.SetValue(target, value);
			}
			catch (Exception ex)
			{
				throw new XamlObjectWriterException($"Set value of member '{member}' threw an exception", ex);
			}
		}

	}
}