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
    public class VariableInventory {
        private Dictionary<string, Scope> _scopeMap;

        public VariableInventory() {
            _scopeMap = new Dictionary<string, Scope>();
        }

        public void AddScopes(IEnumerable<Scope> scopes) {
            foreach(var scope in scopes) {
                AddScope(scope);
            }
        }

        public void AddScope(Scope scope) {
            _scopeMap[scope.PrimaryLocation.XPath] = scope;
        }

        public VariableDeclaration GetDeclarationForUse(VariableUse use) {
            var parentXPath = "";
            VariableDeclaration declaration = null;

            Scope parentScope;
            if(_scopeMap.TryGetValue(parentXPath, out parentScope)) {
                while(declaration == null && parentScope != null) {
                    declaration = (from d in parentScope.DeclaredVariables
                                   where d.Name == use.Name
                                   select d).FirstOrDefault();
                    parentScope = parentScope.ParentScope;
                }
            }
            return declaration;
        }
    }
}