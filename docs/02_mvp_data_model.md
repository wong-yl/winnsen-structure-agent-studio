# MVP Data Model

## Entities

### Project

Represents one engineering model package or product family.

Fields:

- `project_id`
- `name`
- `product_type`
- `source_root`
- `status`
- `created_at`
- `updated_at`
- `notes`

### Source File

Represents a source file discovered during intake.

Fields:

- `file_id`
- `project_id`
- `path`
- `file_type`
- `module`
- `role`
- `status`
- `linked_model`
- `linked_drawing`
- `linked_dxf`
- `notes`

### Module

Represents a structural module.

Fields:

- `module_id`
- `project_id`
- `module_type`
- `name`
- `source_count`
- `rule_count`
- `maturity`
- `manual_review_count`

### Rule Candidate

Represents one reusable geometry or placement rule candidate.

Fields:

- `rule_id`
- `project_id`
- `module_type`
- `rule_family`
- `source_level`
- `evidence_sources`
- `maturity`
- `production_confidence`
- `requires_review`
- `notes`

### Generation Capability

Represents what the system can generate.

Fields:

- `capability_id`
- `product_type`
- `module_type`
- `variant`
- `parameter_schema`
- `generator_script`
- `validation_script`
- `status`
- `latest_validation_report`

### Review Queue Item

Represents an item requiring human or Agent review.

Fields:

- `review_id`
- `project_id`
- `module_type`
- `priority`
- `issue_type`
- `description`
- `next_action`
- `status`

## Maturity Levels

Use these maturity levels consistently:

- `raw_imported`
- `mapped`
- `dxf_parsed`
- `solidworks_extracted`
- `step_bbox_measured`
- `engineering_reference`
- `production_candidate`
- `manual_review_required`
- `blocked`

## Confidence Rule

No row should be marked as `production_candidate` unless it has traceable source evidence and a validation report.
