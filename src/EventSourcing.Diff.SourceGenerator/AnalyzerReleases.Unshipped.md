; Unshipped analyzer release

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
ESDIFF001 | EventSourcing.Diff | Error | Comparison host must be a non-generic top-level static partial class
ESDIFF002 | EventSourcing.Diff | Error | Model type is unsupported or inaccessible
ESDIFF003 | EventSourcing.Diff | Error | Custom comparison has an invalid signature
ESDIFF004 | EventSourcing.Diff | Error | Interface model has an ambiguous inherited property
ESDIFF005 | EventSourcing.Diff | Warning | Ignored property name matches no public instance property
ESDIFF006 | EventSourcing.Diff | Error | MatchBy property is missing, unreadable or a collection
ESDIFF007 | EventSourcing.Diff | Error | Conflicting MatchBy values for the same type
