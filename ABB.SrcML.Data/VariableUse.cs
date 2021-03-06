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
using System.Linq;
using System.Text;

namespace ABB.SrcML.Data {
    /// <summary>
    /// The variable use class represents a use of a variable.
    /// </summary>
    [Serializable]
    public class VariableUse : AbstractUse<VariableDeclaration>, IResolvesToType {
        /// <summary>
        /// The calling object for a use is used when you have <c>a.b</c> -- this variable use would refer to <c>b</c> and the calling object would be <c>a</c>.
        /// </summary>
        public IResolvesToType CallingObject { get; set; }

        /// <summary>
        /// The scope that contains this variable use. If the parent scope is updated, then the parent scope of the calling object is also updated.
        /// </summary>
        public override Scope ParentScope {
            get {
                return base.ParentScope;
            }
            set {
                base.ParentScope = value;
                if(this.CallingObject != null) {
                    this.CallingObject.ParentScope = this.ParentScope;
                }
            }
        }
        /// <summary>
        /// Searches through the <see cref="Scope.DeclaredVariables"/> to see if any of them <see cref="Matches(VariableDeclaration)">matches</see>
        /// </summary>
        /// <returns>An enumerable of matching variable declarations.</returns>
        public override IEnumerable<VariableDeclaration> FindMatches() {
            IEnumerable<VariableDeclaration> matchingVariables = Enumerable.Empty<VariableDeclaration>();

            if(CallingObject != null) {
                matchingVariables = from matchingType in CallingObject.FindMatchingTypes()
                                    from typeDefinition in matchingType.GetParentTypesAndSelf()
                                    from variable in typeDefinition.DeclaredVariables
                                    where Matches(variable)
                                    select variable;
            } else {
                var matches = from scope in ParentScopes
                              from variable in scope.DeclaredVariables
                              where Matches(variable)
                              select variable;

                var parameterMatches = from method in ParentScope.GetParentScopesAndSelf<MethodDefinition>()
                                       from parameter in method.Parameters
                                       where Matches(parameter)
                                       select parameter;

                var matchingParentVariables = from containingType in ParentScope.GetParentScopesAndSelf<TypeDefinition>()
                                              from typeDefinition in containingType.GetParentTypes()
                                              from variable in typeDefinition.DeclaredVariables
                                              where Matches(variable)
                                              select variable;
                matchingVariables = matches.Concat(matchingParentVariables).Concat(parameterMatches);
            }
            return matchingVariables;
        }

        /// <summary>
        /// Tests if this variable usage is a match for <paramref name="definition"/>
        /// </summary>
        /// <param name="definition">The variable declaration to test</param>
        /// <returns>true if this matches the variable declaration; false otherwise</returns>
        public override bool Matches(VariableDeclaration definition) {
            return definition != null && definition.Name == this.Name;
        }

        /// <summary>
        /// Finds all of the matching type definitions for all of the variable declarations that match this variable use
        /// </summary>
        /// <returns>An enumerable of matching type definitions</returns>
        public IEnumerable<TypeDefinition> FindMatchingTypes() {
            IEnumerable<TypeDefinition> typeDefinitions;
            if(this.Name == "this") {
                typeDefinitions = ParentScopes.OfType<TypeDefinition>().Take(1);
            } else if(this.CallingObject != null) {
                typeDefinitions = from typeForCallingObject in this.CallingObject.FindMatchingTypes()
                                  from typeDefinition in typeForCallingObject.GetParentTypesAndSelf()
                                  from variableDeclaration in typeDefinition.DeclaredVariables
                                  where Matches(variableDeclaration)
                                  where variableDeclaration.VariableType != null
                                  from matchingType in variableDeclaration.VariableType.FindMatchingTypes()
                                  select matchingType;
            } else {
                typeDefinitions = from declaration in FindMatches()
                                  where declaration.VariableType != null
                                  from typeDefinition in declaration.VariableType.FindMatches()
                                  select typeDefinition;
            }
            return typeDefinitions;
        }

        /// <summary>
        /// Gets the first result from <see cref="FindMatchingTypes()"/>
        /// </summary>
        /// <returns>The first matching variable type definition</returns>
        public TypeDefinition FindFirstMatchingType() {
            return FindMatchingTypes().FirstOrDefault();
        }
    }
}
