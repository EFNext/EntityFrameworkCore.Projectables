## Release 6.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------------------------------------------------------------------------
EFP0003 | Design   | Warning  | Unsupported statement in block-bodied method                            
EFP0004 | Design   | Error    | Statement with side effects in block-bodied method                      
EFP0005 | Design   | Warning  | Potential side effect in block-bodied method                            
EFP0006 | Design   | Error    | Method or property should expose a body definition (block or expression)
EFP0007 | Design   | Error    | Unsupported pattern in projectable expression
EFP0008 | Design   | Error    | Target class is missing a parameterless constructor
EFP0009 | Design   | Error    | Delegated constructor cannot be analyzed for projection
EFP0010 | Design   | Error    | UseMemberBody target member not found
EFP0011 | Design   | Error    | UseMemberBody target member is incompatible
EFP0012 | Design   | Info     | [Projectable] factory method can be converted to a constructor

### Changed Rules

Rule ID | New Category | New Severity | Old Category | Old Severity | Notes
--------|--------------|--------------|--------------|--------------|-----------------------------------------------------------------
EFP0001 | Design       | Warning      | Design       | Error        | Changed to warning for experimental block-bodied members support

## Release 5.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------------------------------------------------------------------------------
EFP0001 | Design   | Error    | Method or property should expose an expression body definition               
EFP0002 | Design   | Error    | Method or property is not configured to support null-conditional expressions 
