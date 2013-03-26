﻿/******************************************************************************
 * Copyright (c) 2013 ABB Group
 * All rights reserved. This program and the accompanying materials
 * are made available under the terms of the Eclipse Public License v1.0
 * which accompanies this distribution, and is available at
 * http://www.eclipse.org/legal/epl-v10.html
 *
 * Contributors:
 *    Vinay Augustine (ABB Group) - initial API, implementation, & documentation
 *****************************************************************************/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace ABB.SrcML.Data {
    /// <summary>
    /// <para>AbstractCodeParser is used to parse SrcML files and extract useful info from the elements. Implementations of this class provide language-specific functions to extract useful data from the class.</para>
    /// <para>The entry point for this class is the <see cref="ParseFileUnit(XElement)"/> method.</para>
    /// </summary>
    public abstract class AbstractCodeParser {
        /// <summary>
        /// Creates a new abstract code parser object. Should only be called by child classes.
        /// </summary>
        protected AbstractCodeParser() {
            ContainerElementNames = new HashSet<XName>(new XName[] {
                SRC.Block, SRC.Catch, SRC.Class, SRC.Constructor, SRC.ConstructorDeclaration, SRC.Destructor,  SRC.DestructorDeclaration, SRC.Do,
                SRC.Else, SRC.Enum, SRC.Extern, SRC.For, SRC.Function, SRC.FunctionDeclaration, SRC.If, SRC.Namespace, SRC.Struct, SRC.Switch,
                SRC.Template, SRC.Then, SRC.Try, SRC.Typedef, SRC.Union, SRC.Unit, SRC.While,
            });
            MethodElementNames = new HashSet<XName>(new XName[] { SRC.Function, SRC.Constructor, SRC.Destructor,
                                                                  SRC.FunctionDeclaration, SRC.ConstructorDeclaration, SRC.DestructorDeclaration });
            NamespaceElementNames = new HashSet<XName>(new XName[] { SRC.Namespace });
            VariableDeclarationElementNames = new HashSet<XName>(new XName[] { SRC.Declaration, SRC.DeclarationStatement });
            ContainerReferenceElementNames = new HashSet<XName>(new XName[] { SRC.ClassDeclaration, SRC.StructDeclaration, SRC.UnionDeclaration,
                                                                                SRC.ConstructorDeclaration, SRC.DestructorDeclaration, SRC.FunctionDeclaration });
        }

        /// <summary>
        /// Returns the Language that this parser supports
        /// </summary>
        public abstract Language ParserLanguage { get; }

        /// <summary>
        /// Returns the XNames that represent containers for this language
        /// </summary>
        public HashSet<XName> ContainerElementNames { get; protected set; }

        /// <summary>
        /// Returns the XNames that represent types for this language
        /// </summary>
        public HashSet<XName> MethodElementNames { get; protected set; }

        /// <summary>
        /// Returns the XNames that represent namespaces for this language
        /// </summary>
        public HashSet<XName> NamespaceElementNames { get; protected set; }

        /// <summary>
        /// Returns the XNames that represent types for this language
        /// </summary>
        public HashSet<XName> TypeElementNames { get; protected set; }

        /// <summary>
        /// Returns the XNames that represent variable declarations for this language
        /// </summary>
        public HashSet<XName> VariableDeclarationElementNames { get; protected set; }

        /// <summary>
        /// Returns the XNames that represent reference elements (such as function_decl and class_decl)
        /// </summary>
        public HashSet<XName> ContainerReferenceElementNames { get; protected set; }

        /// <summary>
        /// Returns the XName that represents an import statement
        /// </summary>
        public XName AliasElementName { get; protected set; }

        /// <summary>
        /// Parses a file unit and returns a <see cref="NamespaceDefinition.IsGlobal">global</see> <see cref="NamespaceDefinition">namespace definition</see> object
        /// </summary>
        /// <param name="fileUnit">The file unit to parse</param>
        /// <returns>a global namespace definition for <paramref name="fileUnit"/></returns>
        public virtual NamespaceDefinition ParseFileUnit(XElement fileUnit) {
            if(null == fileUnit) throw new ArgumentNullException("fileUnit");
            if(SRC.Unit != fileUnit.Name) throw new ArgumentException("should be a SRC.Unit", "fileUnit");

            var globalScope = ParseElement(fileUnit, new ParserContext()) as NamespaceDefinition;
            return globalScope;
        }

        /// <summary>
        /// This is the main function that parses srcML nodes. It selects the appropriate parse element to call and then adds declarations, method calls, and children to it
        /// </summary>
        /// <param name="element">The element to parse</param>
        /// <param name="context">The parser context</param>
        /// <returns>The scope representing <paramref name="element"/></returns>
        public virtual Scope ParseElement(XElement element, ParserContext context) {
            if(element.Name == SRC.Unit) {
                ParseUnitElement(element, context);
            } else if(TypeElementNames.Contains(element.Name)) {
                ParseTypeElement(element, context);
            } else if(NamespaceElementNames.Contains(element.Name)) {
                ParseNamespaceElement(element, context);
            } else if(MethodElementNames.Contains(element.Name)) {
                ParseMethodElement(element, context);
            } else {
                ParseContainerElement(element, context);
            }

            foreach(var declarationElement in GetDeclarationsFromElement(element)) {
                foreach(var declaration in ParseDeclarationElement(declarationElement, context)) {
                    context.CurrentScope.AddDeclaredVariable(declaration);
                }
            }
            foreach(var methodCallElement in GetMethodCallsFromElement(element)) {
                var methodCall = ParseCallElement(methodCallElement, context);
                context.CurrentScope.AddMethodCall(methodCall);
            }

            foreach(var childElement in GetChildContainers(element)) {
                var childScope = ParseElement(childElement, context);
                context.CurrentScope.AddChildScope(childScope);
            }
            var currentScope = context.Pop();
            currentScope.AddSourceLocation(context.CreateLocation(element, ContainerIsReference(element)));
            currentScope.ProgrammingLanguage = ParserLanguage;

            return currentScope;
        }

        /// <summary>
        /// Creates a <see cref="Scope"/> object for <paramref name="element"/> and pushes it onto <paramref name="context"/>
        /// </summary>
        /// <param name="element">The element to parse</param>
        /// <param name="context">the context to place the resulting scope on</param>
        public virtual void ParseContainerElement(XElement element, ParserContext context) {
            var scope = new Scope();
            context.Push(scope);
        }

        /// <summary>
        /// Creates a <see cref="MethodDefinition"/> object for <paramref name="methodElement"/> and pushes it onto <paramref name="context"/>
        /// </summary>
        /// <param name="methodElement">The element to parse</param>
        /// <param name="context">The context to place the resulting method definition in</param>
        public virtual void ParseMethodElement(XElement methodElement, ParserContext context) {
            if(null == methodElement) throw new ArgumentNullException("methodElement");
            if(!MethodElementNames.Contains(methodElement.Name)) throw new ArgumentException("must be a method typeUseElement", "fileUnit");

            var methodDefinition = new MethodDefinition() {
                Name = GetNameForMethod(methodElement),
                IsConstructor = (methodElement.Name == SRC.Constructor || methodElement.Name == SRC.ConstructorDeclaration),
                IsDestructor = (methodElement.Name == SRC.Destructor || methodElement.Name == SRC.DestructorDeclaration),
                Accessibility = GetAccessModifierForMethod(methodElement),
            };
            var parameters = from paramElement in GetParametersFromMethodElement(methodElement)
                             select ParseMethodParameterElement(paramElement, context);
            foreach(var parameter in parameters) {
                methodDefinition.Parameters.Add(parameter);
            }
            context.Push(methodDefinition);
        }

        /// <summary>
        /// Creates a <see cref="NamespaceDefinition"/> object for <paramref name="namespaceElement"/> and pushes it onto <paramref name="context"/>
        /// </summary>
        /// <param name="namespaceElement">The element to parse</param>
        /// <param name="context">The context to place the resulting namespace definition in</param>
        public abstract void ParseNamespaceElement(XElement namespaceElement, ParserContext context);

        /// <summary>
        /// Parses a type element and pushes a it onto the <paramref name="context"/>.
        /// </summary>
        /// <param name="typeElement">the type element to parse</param>
        /// <param name="context">The parser context</param>
        public virtual void ParseTypeElement(XElement typeElement, ParserContext context) {
            if(null == typeElement) throw new ArgumentNullException("typeElement");

            var typeDefinition = new TypeDefinition() {
                Accessibility = GetAccessModifierForType(typeElement),
                Kind = XNameMaps.GetKindForXElement(typeElement),
                Name = GetNameForType(typeElement),
            };
            foreach(var parentTypeElement in GetParentTypeUseElements(typeElement)) {
                var parentTypeUse = ParseTypeUseElement(parentTypeElement, context);
                typeDefinition.AddParentType(parentTypeUse);
            }
            context.Push(typeDefinition);
        }

        /// <summary>
        /// Creates a global <see cref="NamespaceDefinition"/> object for <paramref name="unitElement"/> and pushes it onto <paramref name="context"/>
        /// </summary>
        /// <param name="unitElement">The element to parse</param>
        /// <param name="context">The context to place the resulting namespace definition in</param>
        public virtual void ParseUnitElement(XElement unitElement, ParserContext context) {
            if(null == unitElement) throw new ArgumentNullException("unitElement");
            if(SRC.Unit != unitElement.Name) throw new ArgumentException("should be a SRC.Unit", "unitElement");
            context.FileUnit = unitElement;
            var aliases = from aliasStatement in GetAliasElementsForFile(unitElement)
                          select ParseAliasElement(aliasStatement, context);

            context.Aliases = new Collection<Alias>(aliases.ToList());

            var namespaceForUnit = new NamespaceDefinition();
            context.Push(namespaceForUnit);
        }

        /// <summary>
        /// Creates an <see cref="Alias"/> object from a using import (such as using in C++ and C# and import in Java).
        /// </summary>
        /// <param name="aliasStatement">The statement to parse. Should be of type <see cref="AliasElementName"/></param>
        /// <param name="context">The context to place the resulting alias in</param>
        /// <returns>a new alias object that represents this alias statement</returns>
        public Alias ParseAliasElement(XElement aliasStatement, ParserContext context) {
            if(null == aliasStatement) throw new ArgumentNullException("aliasStatement");
            if(aliasStatement.Name != AliasElementName) throw new ArgumentException(String.Format("must be a {0} statement", AliasElementName), "usingStatement");

            var alias = new Alias() {
                Location = context.CreateLocation(aliasStatement, true),
                ProgrammingLanguage = ParserLanguage,
            };

            IEnumerable<XElement> namespaceNames = GetNamesFromAlias(aliasStatement);

            if(!AliasIsNamespaceImport(aliasStatement)) {
                var lastNameElement = namespaceNames.LastOrDefault();
                namespaceNames = from name in namespaceNames
                                 where name.IsBefore(lastNameElement)
                                 select name;

                alias.ImportedNamedScope = new NamedScopeUse() {
                    Name = lastNameElement.Value,
                    Location = context.CreateLocation(lastNameElement),
                    ProgrammingLanguage = ParserLanguage,
                };
            }

            NamespaceUse current = null;
            foreach(var namespaceName in namespaceNames) {
                var use = new NamespaceUse() {
                    Name = namespaceName.Value,
                    Location = context.CreateLocation(namespaceName),
                    ProgrammingLanguage = ParserLanguage,
                };

                if(alias.ImportedNamespace == null) {
                    alias.ImportedNamespace = use;
                    current = use;
                } else {
                    current.ChildScopeUse = use;
                    current = use;
                }
            }

            return alias;
        }

        /// <summary>
        /// Creates a method call object
        /// </summary>
        /// <param name="callElement">The XML element to parse</param>
        /// <param name="context">The parser context</param>
        /// <returns>A method call for <paramref name="callElement"/></returns>
        public virtual MethodCall ParseCallElement(XElement callElement, ParserContext context) {
            string name = String.Empty;
            bool isConstructor = false;
            bool isDestructor = false;
            IEnumerable<XElement> callingObjectNames = Enumerable.Empty<XElement>();

            var nameElement = callElement.Element(SRC.Name);
            if(null != nameElement) {
                name = NameHelper.GetLastName(nameElement);
                callingObjectNames = NameHelper.GetNameElementsExceptLast(nameElement);
            }

            var precedingElements = callElement.ElementsBeforeSelf();

            foreach(var pe in precedingElements) {
                if(pe.Name == OP.Operator && pe.Value == "new") {
                    isConstructor = true;
                } else if(pe.Name == OP.Operator && pe.Value == "~") {
                    isDestructor = true;
                }
            }

            var methodCall = new MethodCall() {
                Name = name,
                IsConstructor = isConstructor,
                IsDestructor = isDestructor,
                ParentScope = context.CurrentScope,
                Location = context.CreateLocation(callElement),
            };

            var arguments = from argument in callElement.Element(SRC.ArgumentList).Elements(SRC.Argument)
                            select CreateResolvableUse(argument, context);
            methodCall.Arguments = new Collection<IResolvesToType>(arguments.ToList<IResolvesToType>());

            IResolvesToType current = methodCall;
            // This foreach block gets all of the name elements included in the actual <call> element
            // this is done primarily in C# and Java where they can reliably be included there
            foreach(var callingObjectName in callingObjectNames.Reverse()) {
                var callingObject = this.CreateVariableUse(callingObjectName, context);
                current.CallingObject = callingObject;
                current = callingObject;
            }

            // after getting those, we look at the name elements that appear *before* a call
            // we keep taking name elements as long as they are preceded by "." or "->"
            // we want to accept get 'a', 'b', and 'c' from "a.b->c" only 'b' and 'c' from
            // "a + b->c"
            var elementsBeforeCall = callElement.ElementsBeforeSelf().ToArray();
            int i = elementsBeforeCall.Length - 1;

            while(i > 0 && elementsBeforeCall[i].Name == OP.Operator &&
                  (elementsBeforeCall[i].Value == "." || elementsBeforeCall[i].Value == "->")) {
                i--;
                if(i >= 0 && elementsBeforeCall[i].Name == SRC.Name) {
                    var callingObject = CreateVariableUse(elementsBeforeCall[i], context);
                    current.CallingObject = callingObject;
                    current = callingObject;
                }
            }
            if(methodCall.CallingObject == null) {
                methodCall.AddAliases(context.Aliases);
            }
            // TODO can we add aliases to calling object?
            return methodCall;
        }

        /// <summary>
        /// Creates variable declaration objects from the given declaration element 
        /// </summary>
        /// <param name="declarationElement">The variable declaration to parse. Must belong to <see cref="VariableDeclarationElementNames"/></param>
        /// <param name="context">The parser context</param>
        /// <returns>One variable declaration object for each declaration in <paramref name="declarationElement"/></returns>
        public virtual IEnumerable<VariableDeclaration> ParseDeclarationElement(XElement declarationElement, ParserContext context) {
            if(declarationElement == null) throw new ArgumentNullException("declaration");
            if(!VariableDeclarationElementNames.Contains(declarationElement.Name)) throw new ArgumentException("XElement.Name must be in VariableDeclarationElementNames");

            XElement declElement;
            if(declarationElement.Name == SRC.Declaration || declarationElement.Name == SRC.FunctionDeclaration) {
                declElement = declarationElement;
            } else {
                declElement = declarationElement.Element(SRC.Declaration);
            }

            var typeElement = declElement.Element(SRC.Type);

            var declarationType = ParseTypeUseElement(typeElement, context);

            foreach(var nameElement in declElement.Elements(SRC.Name)) {
                var variableDeclaration = new VariableDeclaration() {
                    VariableType = declarationType,
                    Name = nameElement.Value,
                    Location = context.CreateLocation(nameElement),
                    Scope = context.CurrentScope,
                };
                yield return variableDeclaration;
            }
        }

        /// <summary>
        /// Generates a parameter declaration for the given declaration
        /// </summary>
        /// <param name="declElement">The declaration XElement from within the parameter element. Must be a <see cref="ABB.SrcML.SRC.Declaration"/> or <see cref="ABB.SrcML.SRC.FunctionDeclaration"/></param>
        /// <param name="context">the parser context</param>
        /// <returns>A parameter declaration object</returns>
        public virtual ParameterDeclaration ParseMethodParameterElement(XElement declElement, ParserContext context) {
            if(declElement == null) throw new ArgumentNullException("declElement");
            if(declElement.Name != SRC.Declaration && declElement.Name != SRC.FunctionDeclaration) throw new ArgumentException("must be of element type SRC.Declaration or SRC.FunctionDeclaration", "declElement");

            var typeElement = declElement.Element(SRC.Type);
            var nameElement = declElement.Element(SRC.Name);
            var name = (nameElement == null ? String.Empty : nameElement.Value);

            var parameterDeclaration = new ParameterDeclaration {
                VariableType = ParseTypeUseElement(typeElement, context),
                Name = name,
                Method = context.CurrentScope as MethodDefinition
            };
            parameterDeclaration.Locations.Add(context.CreateLocation(declElement));
            return parameterDeclaration;
        }

        /// <summary>
        /// Creates a type use element
        /// </summary>
        /// <param name="typeUseElement">the element to parse. Must be of a <see cref="ABB.SrcML.SRC.Type"/> or <see cref="ABB.SrcML.SRC.Name"/></param>
        /// <param name="context">the parser context</param>
        /// <returns>A Type Use object</returns>
        public virtual TypeUse ParseTypeUseElement(XElement typeUseElement, ParserContext context) {
            if(typeUseElement == null) throw new ArgumentNullException("typeUseElement");

            XElement typeNameElement;

            // validate the type use typeUseElement (must be a SRC.Name or SRC.Type)
            if(typeUseElement.Name == SRC.Type) {
                typeNameElement = typeUseElement.Element(SRC.Name);
            } else if(typeUseElement.Name == SRC.Name) {
                typeNameElement = typeUseElement;
            } else {
                throw new ArgumentException("typeUseElement should be of type type or name", "typeUseElement");
            }

            XElement lastNameElement = null;
            NamedScopeUse prefix = null;

            if(typeNameElement != null) {
                lastNameElement = NameHelper.GetLastNameElement(typeNameElement);
                prefix = ParseNamedScopeUsePrefix(typeNameElement, context);
            }

            var typeUse = new TypeUse() {
                Name = (lastNameElement != null ? lastNameElement.Value : String.Empty),
                ParentScope = context.CurrentScope,
                Location = context.CreateLocation(lastNameElement != null ? lastNameElement : typeUseElement),
                Prefix = prefix,
                ProgrammingLanguage = this.ParserLanguage,
            };
            typeUse.AddAliases(context.Aliases);
            return typeUse;
        }

        /// <summary>
        /// Gets the type use elements from a <see cref="TypeElementNames">type definition element</see>
        /// </summary>
        /// <param name="typeElement">The type element. Must belong to <see cref="TypeElementNames"/></param>
        /// <returns>An enumerable of type uses that represent parent types</returns>
        public abstract IEnumerable<XElement> GetParentTypeUseElements(XElement typeElement);

        /// <summary>
        /// Creates a named scope use element
        /// </summary>
        /// <param name="nameElement">The name element to parse</param>
        /// <param name="context">The parser context</param>
        /// <returns>A named scope use for this element</returns>
        public NamedScopeUse ParseNamedScopeUsePrefix(XElement nameElement, ParserContext context) {
            IEnumerable<XElement> parentNameElements = Enumerable.Empty<XElement>();

            parentNameElements = NameHelper.GetNameElementsExceptLast(nameElement);
            NamedScopeUse current = null, root = null;

            if(parentNameElements.Any()) {
                foreach(var element in parentNameElements) {
                    var scopeUse = new NamedScopeUse() {
                        Name = element.Value,
                        Location = context.CreateLocation(element, true),
                        ProgrammingLanguage = this.ParserLanguage,
                    };
                    if(null == root) {
                        root = scopeUse;
                    }
                    if(current != null) {
                        current.ChildScopeUse = scopeUse;
                    }
                    current = scopeUse;
                }
            }
            return root;
        }

        /// <summary>
        /// Gets all of the parameters for this method. It finds the variable declarations in parameter list.
        /// </summary>
        /// <param name="method">The method container</param>
        /// <returns>An enumerable of all the declaration XElements.</returns>
        public virtual IEnumerable<XElement> GetParametersFromMethodElement(XElement method) {
            var parameters = from parameter in method.Element(SRC.ParameterList).Elements(SRC.Parameter)
                             let declElement = parameter.Elements().First()
                             select declElement;
            return parameters;
        }

        /// <summary>
        /// Creates a resolvable use from an expression
        /// </summary>
        /// <param name="element">The element to parse</param>
        /// <param name="context">The parser context</param>
        /// <returns>A resolvable use object</returns>
        // TODO make this fit in with the rest of the parse methods (rename to parse)
        public virtual IResolvesToType CreateResolvableUse(XElement element, ParserContext context) {
            var use = new VariableUse() {
                Location = context.CreateLocation(element, true),
                ParentScope = context.CurrentScope,
                ProgrammingLanguage = ParserLanguage,
            };
            return use;
        }
        
        /// <summary>
        /// Creates a variable use from the given element. Must be a <see cref="ABB.SrcML.SRC.Expression"/>, <see cref="ABB.SrcML.SRC.Name"/>, or <see cref="ABB.SrcML.SRC.ExpressionStatement"/>
        /// </summary>
        /// <param name="element">The element to parse</param>
        /// <param name="context">The parser context</param>
        /// <returns>A variable use object</returns>
        // TODO make this fit in with the rest of the parse methods
        public virtual VariableUse CreateVariableUse(XElement element, ParserContext context) {
            XElement nameElement;
            if(element.Name == SRC.Name) {
                nameElement = element;
            } else if(element.Name == SRC.Expression) {
                nameElement = element.Element(SRC.Name);
            } else if(element.Name == SRC.ExpressionStatement || element.Name == SRC.Argument) {
                nameElement = element.Element(SRC.Expression).Element(SRC.Name);
            } else {
                throw new ArgumentException("element should be an expression, expression statement, argument, or name", "element");
            }

            var lastNameElement = NameHelper.GetLastNameElement(nameElement);

            var variableUse = new VariableUse() {
                Location = context.CreateLocation(lastNameElement, true),
                Name = lastNameElement.Value,
                ParentScope = context.CurrentScope,
                ProgrammingLanguage = ParserLanguage,
            };
            return variableUse;
        }

        #region aliases
        /// <summary>
        /// Checks if this alias statement is a namespace import or something more specific (such as a type or method)
        /// </summary>
        /// <param name="aliasStatement">The alias statement to check. Must be of type <see cref="AliasElementName"/></param>
        /// <returns>True if this is a namespace import; false otherwise</returns>
        public abstract bool AliasIsNamespaceImport(XElement aliasStatement);

        /// <summary>
        /// Gets all of the names for this alias
        /// </summary>
        /// <param name="aliasStatement">The alias statement. Must be of type <see cref="AliasElementName"/></param>
        /// <returns>An enumerable of all the <see cref="ABB.SrcML.SRC.Name">name elements</see> for this statement</returns>
        public virtual IEnumerable<XElement> GetNamesFromAlias(XElement aliasStatement) {
            if(null == aliasStatement) throw new ArgumentNullException("aliasStatement");
            if(aliasStatement.Name != AliasElementName) throw new ArgumentException(String.Format("should be an {0} statement", AliasElementName), "aliasStatement");

            var nameElement = aliasStatement.Element(SRC.Name);
            if(null != nameElement)
                return NameHelper.GetNameElementsFromName(nameElement);
            return Enumerable.Empty<XElement>();
        }
        #endregion

        /// <summary>
        /// Gets the alias elements for this file. This only returns the aliases at the root of the file
        /// </summary>
        /// <param name="fileUnit">The file unit to get the aliases from</param>
        /// <returns>The alias elements</returns>
        // TODO handle alias elements in other parts of the file
        public virtual IEnumerable<XElement> GetAliasElementsForFile(XElement fileUnit) {
            if(null == fileUnit) throw new ArgumentNullException("fileUnit");
            if(SRC.Unit != fileUnit.Name) throw new ArgumentException("must be a unit element", "fileUnit");

            return fileUnit.Elements(AliasElementName);
        }
        
        #region get child containers from scope
        /// <summary>
        /// Gets all of the child containers for the given container
        /// </summary>
        /// <param name="container">The container</param>
        /// <returns>An enumerable of all the children</returns>
        public virtual IEnumerable<XElement> GetChildContainers(XElement container) {
            if(null == container) return Enumerable.Empty<XElement>();
            IEnumerable<XElement> children;

            if(TypeElementNames.Contains(container.Name)) {
                children = GetChildContainersFromType(container);
            } else if(MethodElementNames.Contains(container.Name)) {
                children = GetChildContainersFromMethod(container);
            } else if(NamespaceElementNames.Contains(container.Name)) {
                children = GetChildContainersFromNamespace(container);
            } else {
                children = from child in container.Elements()
                           where ContainerElementNames.Contains(child.Name)
                           select child;
            }
            return children;
        }

        /// <summary>
        /// Gets all of the child containers for a namespace. It calls <see cref="GetChildContainers(XElement)"/> on the child block.
        /// </summary>
        /// <param name="container">The namespace container</param>
        /// <returns>All of the child containers</returns>
        public virtual IEnumerable<XElement> GetChildContainersFromNamespace(XElement container) {
            var block = container.Element(SRC.Block);
            return GetChildContainers(block);
        }

        /// <summary>
        /// Gets all of the child containers for a method. It calls <see cref="GetChildContainers(XElement)"/> on the child block.
        /// </summary>
        /// <param name="container">The method container</param>
        /// <returns>All of the child containers</returns>
        public virtual IEnumerable<XElement> GetChildContainersFromMethod(XElement container) {

            var block = container.Element(SRC.Block);
            return GetChildContainers(block);
        }

        /// <summary>
        /// Gets all of the child containers for a type. It calls <see cref="GetChildContainers(XElement)"/> on the child block.
        /// </summary>
        /// <param name="container">The namespace type</param>
        /// <returns>All of the child containers</returns>
        public virtual IEnumerable<XElement> GetChildContainersFromType(XElement container) {
            var block = container.Element(SRC.Block);
            return GetChildContainers(block);
        }
        #endregion

        #region get method calls from scope
        /// <summary>
        /// Gets the method calls from an element
        /// </summary>
        /// <param name="element">The element to search</param>
        /// <returns>All of the call elements from the element</returns>
        public virtual IEnumerable<XElement> GetMethodCallsFromElement(XElement element) {
            if(MethodElementNames.Contains(element.Name) ||
               NamespaceElementNames.Contains(element.Name) ||
               TypeElementNames.Contains(element.Name) ||
                element.Name == SRC.Unit) {
                return GetCallsFromBlockParent(element);
            }
            return GetMethodCallsFromBlockElement(element);
        }

        private IEnumerable<XElement> GetCallsFromBlockParent(XElement container) {
            var block = container.Element(SRC.Block);
            if(null == block)
                return Enumerable.Empty<XElement>();
            return GetMethodCallsFromBlockElement(block);
        }

        private IEnumerable<XElement> GetMethodCallsFromBlockElement(XElement container) {
            var methodCalls = from child in container.Elements()
                              where !ContainerElementNames.Contains(child.Name)
                              from call in child.Descendants(SRC.Call)
                              select call;
            return methodCalls;
        }

        #endregion get method calls from scope

        #region get declarations from scope
        /// <summary>
        /// Gets the declaration elements from an element
        /// </summary>
        /// <param name="element">The element to search</param>
        /// <returns>All of the declaration elements from an element</returns>
        public virtual IEnumerable<XElement> GetDeclarationsFromElement(XElement element) {
            if(null == element) return Enumerable.Empty<XElement>();

            IEnumerable<XElement> declarationElements;

            if(SRC.Block == element.Name || SRC.Unit == element.Name) {
                declarationElements = GetDeclarationsFromBlockElement(element);
            } else if(SRC.Catch == element.Name) {
                declarationElements = GetDeclarationsFromCatchElement(element);
            } else if(SRC.For == element.Name) {
                declarationElements = GetDeclarationsFromForElement(element);
            } else if(MethodElementNames.Contains(element.Name)) {
                declarationElements = GetDeclarationsFromMethodElement(element);
            } else if(TypeElementNames.Contains(element.Name)) {
                declarationElements = GetDeclarationsFromTypeElement(element);
            }else {
                declarationElements = Enumerable.Empty<XElement>();
            }

            return declarationElements;
        }

        /// <summary>
        /// Gets all of the variable declarations for this catch block. It finds the variable declarations in <see cref="ABB.SrcML.SRC.ParameterList"/>.
        /// </summary>
        /// <param name="container">The catch container</param>
        /// <returns>An enumerable of all the declaration XElements.</returns>
        public virtual IEnumerable<XElement> GetDeclarationsFromCatchElement(XElement container) {
            var declarations = from parameter in container.Elements(SRC.Parameter)
                               let declElement = parameter.Element(SRC.Declaration)
                               let typeElement = declElement.Element(SRC.Type)
                               where typeElement != null
                               where !typeElement.Elements(TYPE.Modifier).Any()
                               select declElement;
            return declarations;
        }

        /// <summary>
        /// Gets all of the variable declarations for this block.
        /// </summary>
        /// <param name="container">The type container</param>
        /// <returns>An enumerable of all the declaration XElements.</returns>
        public virtual IEnumerable<XElement> GetDeclarationsFromBlockElement(XElement container) {
            if(null == container) return Enumerable.Empty<XElement>();
            var declarations = from stmtElement in container.Elements(SRC.DeclarationStatement)
                               let declElement = stmtElement.Element(SRC.Declaration)
                               select declElement;
            return declarations;
        }

        /// <summary>
        /// Gets all of the variable declarations for this for loop. It finds the variable declaration in the <see cref="ABB.SrcML.SRC.Init"/> statement.
        /// </summary>
        /// <param name="container">The type container</param>
        /// <returns>An enumerable of all the declaration XElements.</returns>
        public virtual IEnumerable<XElement> GetDeclarationsFromForElement(XElement container) {
            var declarations = from declElement in container.Element(SRC.Init).Elements(SRC.Declaration)
                               select declElement;
            return declarations;
        }

        /// <summary>
        /// Gets all of the variable declarations for this method. It finds the variable declarations in the child block.
        /// </summary>
        /// <param name="container">The method container</param>
        /// <returns>An enumerable of all the declaration XElements.</returns>
        public virtual IEnumerable<XElement> GetDeclarationsFromMethodElement(XElement container) {
            var block = container.Element(SRC.Block);
            return GetDeclarationsFromBlockElement(block);
        }

        /// <summary>
        /// Gets all of the variable declarations for this type. It finds the variable declarations in the child block.
        /// </summary>
        /// <param name="container">The type container</param>
        /// <returns>An enumerable of all the declaration XElements.</returns>
        public virtual IEnumerable<XElement> GetDeclarationsFromTypeElement(XElement container) {
            var block = container.Element(SRC.Block);
            foreach(var declElement in GetDeclarationsFromBlockElement(block)) {
                yield return declElement;
            }
        }
        #endregion get declarations from scope

        #region access modifiers
        /// <summary>
        /// Gets the access modifier for this method. For Java and C#, a "specifier" tag is placed in either
        /// the method callElement, or the type callElement in the method.
        /// </summary>
        /// <param name="methodElement">The method callElement</param>
        /// <returns>The first specifier encountered. If none, it returns <see cref="AccessModifier.None"/></returns>
        public virtual AccessModifier GetAccessModifierForMethod(XElement methodElement) {
            Dictionary<string, AccessModifier> accessModifierMap = new Dictionary<string, AccessModifier>() {
                { "public", AccessModifier.Public },
                { "private", AccessModifier.Private },
                { "protected", AccessModifier.Protected },
                { "internal", AccessModifier.Internal },
            };

            var specifierContainer = methodElement.Element(SRC.Type);
            if(null == specifierContainer) {
                specifierContainer = methodElement;
            }

            var specifiers = from specifier in specifierContainer.Elements(SRC.Specifier)
                             where accessModifierMap.ContainsKey(specifier.Value)
                             select accessModifierMap[specifier.Value];

            return (specifiers.Any() ? specifiers.First() : AccessModifier.None);
        }

        /// <summary>
        /// Gets the access modifier for the given type
        /// </summary>
        /// <param name="typeElement">The type callElement</param>
        /// <returns>The access modifier for the type.</returns>
        public abstract AccessModifier GetAccessModifierForType(XElement typeElement);
        #endregion access modifiers

        #region parse literal types
        /// <summary>
        /// Parses a literal use element
        /// </summary>
        /// <param name="literalElement">The literal element to parse</param>
        /// <param name="context">The parser context</param>
        /// <returns>A literal use object</returns>
        public virtual LiteralUse ParseLiteralElement(XElement literalElement, ParserContext context) {
            if(literalElement == null) throw new ArgumentNullException("literalElement");
            if(literalElement.Name != LIT.Literal) throw new ArgumentException("should be a literal", "literalElement");

            var kind = LiteralUse.GetLiteralKind(literalElement);
            string typeName = string.Empty;


            var use = new LiteralUse() {
                Kind = kind,
                Location = context.CreateLocation(literalElement),
                Name = GetTypeForLiteralValue(kind, literalElement.Value),
                ParentScope = context.CurrentScope,
            };

            return use;
        }

        /// <summary>
        /// Gets the type of the literal element
        /// </summary>
        /// <param name="kind">The literal kind</param>
        /// <param name="literalValue">The value</param>
        /// <returns>The name of this type</returns>
        public virtual string GetTypeForLiteralValue(LiteralKind kind, string literalValue) {
            switch(kind) {
                case LiteralKind.Boolean:
                    return GetTypeForBooleanLiteral(literalValue);
                case LiteralKind.Character:
                    return GetTypeForCharacterLiteral(literalValue);
                case LiteralKind.Number:
                    return GetTypeForNumberLiteral(literalValue);
                case LiteralKind.String:
                    return GetTypeForStringLiteral(literalValue);
            }
            return String.Empty;
        }

        /// <summary>
        /// Gets the type for a boolean literal
        /// </summary>
        /// <param name="literalValue">The literal value to parse</param>
        /// <returns>The type name</returns>
        public abstract string GetTypeForBooleanLiteral(string literalValue);

        /// <summary>
        /// Gets the type for a character literal
        /// </summary>
        /// <param name="literalValue">the literal value to parse</param>
        /// <returns>The type name</returns>
        public abstract string GetTypeForCharacterLiteral(string literalValue);

        /// <summary>
        /// Gets the type for a number literal
        /// </summary>
        /// <param name="literalValue">The literal value to parse</param>
        /// <returns>The type name</returns>
        public abstract string GetTypeForNumberLiteral(string literalValue);

        /// <summary>
        /// Gets the type for a string literal
        /// </summary>
        /// <param name="literalValue">The literal value to parse</param>
        /// <returns>The type name</returns>
        public abstract string GetTypeForStringLiteral(string literalValue);
        #endregion

        #region utilities
        /// <summary>
        /// Checks to see if this callElement is a reference container
        /// </summary>
        /// <param name="element">The callElement to check</param>
        /// <returns>True if this is a reference container; false otherwise</returns>
        public virtual bool ContainerIsReference(XElement element) {
            return (element != null && ContainerReferenceElementNames.Contains(element.Name));
        }

        /// <summary>
        /// Gets the filename for the given file unit.
        /// </summary>
        /// <param name="fileUnit">The file unit. <c>fileUnit.Name</c> must be <c>SRC.Unit</c></param>
        /// <returns>The file path represented by this <paramref name="fileUnit"/></returns>
        public virtual string GetFileNameForUnit(XElement fileUnit) {
            if(fileUnit == null)
                throw new ArgumentNullException("fileUnit");
            if(fileUnit.Name != SRC.Unit)
                throw new ArgumentException("element must be a unit", "fileUnit");

            var fileNameAttribute = fileUnit.Attribute("filename");

            if(null != fileNameAttribute)
                return fileNameAttribute.Value;
            return String.Empty;
        }

        /// <summary>
        /// Gets the name for the method callElement
        /// </summary>
        /// <param name="methodElement">the method callElement to get the name for</param>
        /// <returns>The name of the method</returns>
        public virtual string GetNameForMethod(XElement methodElement) {
            var name = methodElement.Element(SRC.Name);
            if(null == name)
                return string.Empty;
            return name.Value;
        }

        /// <summary>
        /// Gets the name for the type element
        /// </summary>
        /// <param name="typeElement">The type element to get the name for</param>
        /// <returns>The name of the type</returns>
        public virtual string GetNameForType(XElement typeElement) {
            var name = typeElement.Element(SRC.Name);
            if(null == name)
                return string.Empty;
            return name.Value;
        }

        /// <summary>
        /// Gets all of the text nodes that are children of the given element.
        /// </summary>
        /// <param name="element">The element</param>
        /// <returns>An enumerable of the XText elements for <paramref name="element"/></returns>
        public IEnumerable<XText> GetTextNodes(XElement element) {
            var textNodes = from node in element.Nodes()
                            where node.NodeType == XmlNodeType.Text
                            let text = node as XText
                            select text;
            return textNodes;
        }

        #endregion utilities
    }
}
