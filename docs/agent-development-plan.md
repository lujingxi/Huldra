# Huldra Agent Development Plan

> **Status:** Active
> **Plan ownership:** This is a working document maintained alongside Huldra's implementation. Update progress and settled decisions directly to match the current code and the developer's instructions. Discuss unresolved architectural choices before treating them as settled.
>
> **Source synchronization:** The local project files are the source of truth. Inspect the current working directory before making changes and work in the existing checkout by default. Creating a branch, pushing changes, or switching checkouts is only done when the developer asks.

---

## 1. Project Goals

Rebuild `Huldra.Agent` around the current Agent architecture agreed during design discussions.

The new Agent system must:

* Treat Roles as user-definable Markdown documents.
* Keep Role objects intentionally minimal.
* Treat Workers as runtime-created execution instances.
* Make Conductor / Orchestrator a Worker rather than a special hard-coded runtime component.
* Allow an Assistant Worker to act as the default conversational entry point.
* Allow Workers to request other Workers, while Runtime controls whether Worker Control is authorized.
* Use generic, capability-oriented Tools rather than many narrowly specialized Tools.
* Put security enforcement outside the LLM.
* Support `Allow`, `Deny`, `Ask`, and `Auto` security decisions.
* Support App, Project, and Conversation security policy scopes.
* Support simple and advanced security settings.
* Record Runtime-level audit events.
* Design the architecture so localization can be added without restructuring the domain model.
* Leave clean extension boundaries for future plugins without implementing the complete plugin system initially.
* Support both local and online LLMs.
* Treat Worker context as runtime-generated and token-budget-aware.
* Store persistent Agent data in SQLite through the planned Microsoft-supported SQLite integration.

---

# 2. Architectural Principles

## 2.1 LLM Is Never a Security Boundary

The LLM may request an operation, but it cannot authorize or execute the operation by itself.

All potentially consequential operations must pass through Runtime.

```text
Worker
  ↓
Tool Request
  ↓
Runtime
  ↓
Security / Policy
  ↓
Execution
```

---

## 2.2 Role Is Semantic Definition

A Role is defined exclusively by a Markdown file.

The C# `Role` model must not contain hard-coded semantic knowledge such as:

* `IsConductor`
* `IsExecutor`
* `IsCoreRole`
* `AllowedTools`
* `SystemPrompt`
* hard-coded role descriptions

The Role model should primarily identify:

```text
Name
DefinitionPath
```

The filename is the Role name.

Example:

```text
roles/
├── assistant.md
├── conductor.md
├── planner.md
├── researcher.md
└── executor.md
```

User-created Roles must work without code changes.

---

## 2.3 Worker Is a Runtime Object

A Worker is created by Runtime from a `WorkerRequest`.

Workers have a fixed C# structure regardless of the Role they use.

```text
Role
  ↓
WorkerRequest
  ↓
Runtime validation / authorization
  ↓
Worker
```

A Worker contains the capabilities actually granted by Runtime, not merely those requested by another Worker.

---

## 2.4 Worker Creation Uses Requests

Conductor / Orchestrator must never directly instantiate another Worker.

Instead it submits a Worker Request.

```text
Conductor
  ↓
WorkerRequest
  ↓
Runtime
  ├── validate
  ├── security policy
  ├── resource availability
  └── tool authorization
  ↓
Worker
```

A Worker may request Worker Control, but Runtime decides whether that capability is permitted.

---

## 2.5 Requested Capabilities Are Not Granted Capabilities

A Worker Request may contain requested Tools.

Runtime evaluates those requests and creates the Worker with the resulting granted Tools.

```text
Requested Tools
       ↓
Runtime Policy
       ↓
Granted Tools
```

A Worker cannot grant itself additional capabilities.

---

# 3. Development Phases

### Phase 0 — Repository Baseline and Cleanup

**Status: Completed**

#### Completed
- [x] Establish the current repository baseline and verify the active project structure.
- [x] Remove the obsolete Agent architecture, including the old orchestrator, dynamic role executor, role definition model, legacy tool interface, and conflicting tool implementations.
- [x] Remove obsolete services and models that conflict with the new architecture.
- [x] Remove the unnecessary `HtmlAgilityPack` dependency.
- [x] Preserve the `Huldra.Agent` project and its required project configuration.
- [x] Preserve the Markdown-based default role definitions under `Resources/DefaultRoles`.
- [x] Establish the initial domain model files for the new architecture.
- [x] Verify that `Huldra.Agent` builds successfully.

#### Completion Notes
The old orchestration, service, and tool implementation has been removed from the `Huldra.Agent` project. The project now contains the initial domain models and Markdown role resources, providing a clean foundation for the new Runtime-oriented architecture.

The existence of initial domain model files does not mean those models have completed design review or integration testing. These activities belong to Phase 1 and subsequent phases.

---

# 4. Phase 1 — Core Domain Models

**Status: In progress**

Initial model files have been created under `Huldra.Agent/Models`. Review the existing implementations against the agreed architecture before adding or changing models.

#### Review Scope
- [ ] `Role`
- [ ] `WorkerRequest`
- [ ] `Worker`
- [ ] `WorkerContext`
- [ ] `WorkerStatus`
- [ ] `ContextMessage`
- [ ] `ToolRequest`
- [ ] `ToolResult`
- [ ] `SecurityPolicy`
- [ ] `SecurityRule`
- [ ] `SecurityCategory`
- [ ] `SecurityDecision`
- [ ] `SecurityDecisionKind`
- [ ] `AuditEvent`

#### Review Requirements
- `Role` remains minimal: role name and Markdown definition path.
- Requested tools and granted tools remain distinct.
- Workers cannot grant themselves permissions or create other Workers directly.
- Security decisions use stable, language-neutral identifiers.
- Audit events contain structured information and do not store LLM chain-of-thought.
- Domain models remain independent of SQLite, UI, and concrete LLM provider implementations.
- Model responsibilities, nullability, collection mutability, and lifecycle semantics are reviewed before the domain model phase is declared complete.

#### Completion Criteria
All core domain models have been reviewed and corrected where necessary. Their responsibilities and invariants are documented, and the model layer builds successfully without introducing Runtime, UI, persistence, or provider-specific dependencies prematurely.


## Role

```text
Role
├── Name
└── DefinitionPath
```

No hard-coded semantic role types.

---

## WorkerRequest

Represents a request to create a Worker.

Initial conceptual fields:

```text
Role
Model
RequestedTools
Task
```

Additional fields may be introduced only when required by later architecture.

---

## Worker

Represents a Runtime-created Worker.

Initial conceptual fields:

```text
Id
Role
Model
GrantedTools
Context
Status
```

---

## WorkerContext

Represents the context supplied to a Worker.

It must be possible to construct the context independently of the Worker implementation.

---

## ToolRequest

Represents a request from a Worker to invoke a Tool.

---

## ToolResult

Represents the Runtime result of Tool execution.

---

## SecurityPolicy

Represents effective security configuration.

Support:

```text
Allow
Deny
Ask
Auto
```

---

## SecurityRule

Represents a rule for a security category.

Security rules should operate on capabilities / operation categories rather than requiring one rule for every individual Tool.

---

## SecurityDecision

Represents the Runtime security decision.

---

## AuditEvent

Represents a Runtime-generated audit record.

Audit events must be generated by Runtime rather than by the LLM.

---

# 5. Phase 2 — Interfaces and Runtime Boundaries

Introduce interfaces after the domain model is stable.

Initial boundaries:

```text
ITool
IRoleProvider
IWorkerRuntime
ISecurityPolicyEvaluator
IAuditStore
```

The interfaces should remain narrow.

Avoid creating large "god interfaces".

---

# 6. Phase 3 — Role System

Implement Markdown Role discovery and loading.

Requirements:

* Roles are discovered dynamically.
* Role names come from filenames.
* User-defined Roles require no code changes.
* Role content remains free-form Markdown.
* Role content does not grant permissions.
* Role content does not grant Tool access.
* Role content does not grant Worker Control.
* Runtime remains responsible for actual capabilities.

Default `assistant` Role should be provided as part of Huldra's built-in resources.

---

# 7. Phase 4 — Tool System

Implement the capability-oriented Tool architecture.

Initial Tool set:

```text
Conversation
├── conversation.search
└── conversation.read

Project / File
├── file.list
├── file.read
├── file.write
└── file.delete

Terminal
└── terminal.execute

Worker
└── worker.request

Task
└── task.update

Web
├── web.search
└── web.read
```

This list is provisional and may change after implementation.

Tools should expose capabilities rather than one Tool for every small variation.

Example:

```text
file.read
```

should support appropriate parameters for:

* reading the whole file
* reading the first N lines
* reading the last N lines
* reading a specific range
* obtaining file metadata / line count where appropriate

Avoid magic parameters where explicit structured parameters make Tool semantics clearer to LLMs.

---

# 8. Phase 5 — Security System

Implement Runtime security enforcement.

## Security Decisions

```text
Allow
Deny
Ask
Auto
```

`Auto` delegates the decision to a dedicated Evaluator.

```text
Worker
  ↓
Tool Request
  ↓
Runtime
  ↓
Security Policy
  ↓
Auto
  ↓
Evaluator
  ↓
Allow / Deny / Ask
```

Evaluator must not be able to modify its own security permissions.

---

## Security Categories

Initial categories:

```text
File Access
File Modification
File Deletion
Terminal Execution
Network Access
External Data Transmission
Sensitive Data Access
Worker Control
```

The category model should remain extensible.

---

## Security Scope

Policies exist at three levels:

```text
App
Project
Conversation
```

Effective precedence:

```text
Conversation
    ↓
Project
    ↓
App
```

More specific scopes may override broader scopes unless the broader rule is locked.

---

## Simple / Advanced UI Support

Security configuration must support:

### Simple

A small number of high-level security profiles / overall behavior.

### Advanced

Explicit configuration of individual security categories.

The domain model must support both without requiring different security systems.

---

# 9. Phase 6 — Audit System

Implement Runtime-level audit logging.

Audit events must record enough information to reconstruct what happened without depending on LLM-generated logs.

Important fields include:

```text
EventId
Timestamp
ProjectId
ConversationId
ExecutionId
WorkerId
Role
Tool
Operation
Target
SecurityPolicy
Decision
DecisionSource
UserApproval
Result
ReasonCode
```

The exact schema may evolve.

Audit events should distinguish:

```text
Requested
Authorized / Denied
Executed
Result
```

Audit logging must occur even when an operation is denied.

Workers must not directly modify the Audit Store.

---

# 10. Phase 7 — Persistence

Use SQLite as the persistent Agent data store.

Initial logical data areas:

```text
Conversations
Messages
Projects
Plans
Tasks
Workers
ToolCalls
SecurityEvents
AuditEvents
```

Exact schema will be designed after the domain models stabilize.

The persistence layer must not leak SQLite-specific details into the Agent domain model.

---

# 11. Phase 8 — Context Management

Implement Runtime-controlled Worker context construction.

The initial context strategy is:

1. Determine the available context budget.
2. Account for system instructions.
3. Account for Role content.
4. Account for Tool schemas.
5. Account for project context.
6. Reserve output space.
7. Fill the remaining budget with the maximum amount of recent raw conversation history that fits.
8. Do not semantically filter recent history before providing it to the Worker.
9. If the Worker determines that additional historical information is needed, it may use:

   * `conversation.search`
   * `conversation.read`

Context construction must be token-budget-aware rather than based on a fixed number of conversation turns.

Tokenizer / context-counting implementation must account for the fact that different models and providers may use different tokenization and message formatting.

---

# 12. Phase 9 — Assistant and Conversation Modes

Implement the default Assistant entry point.

Conversation Mode:

```text
Automatic
User Defined
```

Automatic:

```text
User
  ↓
Assistant
  ↓
simple task → handle directly

complex task
  ↓
team / orchestrated execution
```

User Defined allows the user to explicitly specify one or more Roles.

The Assistant remains a Worker rather than becoming a hard-coded special Agent class.

---

# 13. Phase 10 — Project / Workspace Integration

Implement the relationship:

```text
Project 1 : N Conversations
```

A Project may have multiple Conversations.

Runtime must recognize Project-level active work.

Example:

```text
Project A
├── Conversation A
│    └── active execution
│
└── Conversation B
     └── sees Project A is active
```

If Conversation B requests additional work while the Project is already actively executing another task, Runtime must recognize the existing Project execution state instead of blindly starting conflicting work.

Workspace mode will expose Project execution / plan / progress information.

---

# 14. Phase 11 — Plugin Extension Boundaries

Do not implement a full plugin marketplace / plugin manager during the initial Agent rewrite.

Instead, preserve clean extension boundaries.

Potential future extension points:

```text
Tools
Model Providers
Role Providers
Context Providers
Evaluators
Storage Providers
UI extensions
```

Third-party plugins must never bypass Runtime security.

Conceptually:

```text
Plugin
  ↓
Huldra Plugin API
  ↓
Runtime
  ↓
Security Policy
  ↓
Execution
```

Plugin-requested capabilities are requests, not automatic permissions.

---

# 15. Phase 12 — Localization Readiness

Initial implementation language:

```text
English
```

However, all user-facing text must be localization-ready.

Rules:

* Do not hard-code UI display text into domain models.
* Use stable identifiers for Tools, Roles, security decisions, etc.
* Keep display strings in resources.
* Do not translate user-defined Role names automatically.
* Do not use localized display text as internal identifiers.

Example:

```text
SecurityDecision.Allow
```

may display as:

```text
Allow
```

or:

```text
允許
```

without changing the underlying domain value.

---

# 16. Phase 13 — Integration Tests

Before connecting real LLM execution, verify the Runtime independently.

Minimum scenarios:

### Worker creation

* [ ] Runtime can create a Worker from a Role.
* [ ] User-defined Roles work.
* [ ] Worker receives only granted Tools.

### Worker Control

* [ ] Worker without Worker Control cannot create Workers.
* [ ] Worker with Worker Control can submit a Worker Request.
* [ ] Runtime can deny Worker Control.
* [ ] User authorization can permit Worker Control.
* [ ] User-created Project configuration can explicitly authorize Worker Control.

### Tool Security

* [ ] Allowed operation executes.
* [ ] Denied operation never executes.
* [ ] Ask operation requests user approval.
* [ ] Auto operation invokes Evaluator.
* [ ] Evaluator cannot bypass Runtime.

### Audit

* [ ] Tool requests are logged.
* [ ] Denied operations are logged.
* [ ] User approvals are logged.
* [ ] Evaluator decisions are logged.
* [ ] Tool execution results are logged.

### Context

* [ ] Recent raw conversation is included up to budget.
* [ ] Tool schemas are accounted for.
* [ ] Output reservation is respected.
* [ ] Additional history can be retrieved through conversation tools.

---

# 17. Phase 14 — Real LLM Integration

Only after the Runtime foundation and security tests are stable:

* [ ] Integrate local LLM Worker execution.
* [ ] Integrate online LLM Worker execution.
* [ ] Implement model-specific token counting / estimation.
* [ ] Implement Worker lifecycle.
* [ ] Implement Assistant execution.
* [ ] Implement Conductor / Orchestrator Worker.
* [ ] Implement Planner / Researcher / Executor workflows.
* [ ] Implement Evaluator Worker.

Local LLM Workers may be recreated between tasks because of hardware / single-model resource constraints.

Online LLM Workers may remain alive longer where appropriate.

The Agent architecture must not depend on either lifecycle strategy.

---

# 18. Phase 15 — Performance and Optimization

After correctness:

* [ ] Reduce unnecessary context construction.
* [ ] Reduce Tool schema overhead.
* [ ] Optimize SQLite queries.
* [ ] Optimize conversation search.
* [ ] Optimize Worker creation.
* [ ] Reduce unnecessary Worker lifetime.
* [ ] Measure local LLM context overhead.
* [ ] Measure online LLM request overhead.
* [ ] Optimize Workspace progress updates.

Performance optimization must not weaken security boundaries or auditability.

---

# 19. Completion Criteria

The new Agent foundation is considered complete when:

* Roles are fully user-definable through Markdown.
* Workers are Runtime-controlled instances.
* Conductor / Orchestrator is implemented as a Worker.
* Assistant is implemented as a Worker and can act as the default conversational entry point.
* Workers cannot bypass Runtime to obtain capabilities.
* Tools are capability-oriented and generic.
* Security policy supports Allow / Deny / Ask / Auto.
* Security policy supports App / Project / Conversation scopes.
* Evaluator decisions are Runtime-mediated.
* Dangerous operations are enforceable independently of LLM instructions.
* Audit events are generated by Runtime.
* Conversation history is persistently stored and searchable.
* Context construction is token-budget-aware.
* Plugin and localization extension boundaries exist without requiring their complete feature implementations.
* Core Agent behavior is covered by automated tests.

---

# 20. Development Rule

The implementation should proceed from:

```text
Domain Model
    ↓
Interfaces
    ↓
Runtime
    ↓
Security
    ↓
Persistence
    ↓
Context
    ↓
LLM
    ↓
UI
```

Do not prematurely implement higher layers when the lower-layer contract is still unstable.

Keep this plan synchronized with the implementation: update progress and completion notes in the same development iteration as the related code changes. When an implementation request leaves a significant architectural choice unresolved, explain the options and get the developer's direction before committing to one.
