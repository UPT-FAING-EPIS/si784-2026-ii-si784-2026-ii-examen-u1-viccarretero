terraform {
  required_version = ">= 1.6.0"

  required_providers {
    github = {
      source  = "integrations/github"
      version = "~> 6.0"
    }
  }
}

variable "github_token" {
  type      = string
  sensitive = true
}

provider "github" {
  owner = "UPT-FAING-EPIS"
  token = var.github_token
}

resource "github_repository_file" "infraestructura" {
  repository          = "si784-2026-ii-si784-2026-ii-examen-u1-viccarretero"
  branch              = "main"
  file                = "terraform-provisioned.txt"
  content             = "Infraestructura aprovisionada automaticamente con Terraform y GitHub Actions."
  commit_message      = "Terraform: aprovisionamiento de infraestructura"
  overwrite_on_create = true
}
