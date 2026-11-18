"""Esquemas de /homologation/suggest. Solo etiquetas de cuentas: nunca datos personales."""

from pydantic import BaseModel, Field

from app.engines.homologation.categories import Category


class AccountIn(BaseModel):
    id: str | None = Field(default=None, max_length=100)
    label: str = Field(max_length=500)
    normalized_label: str | None = Field(default=None, max_length=500)
    sheet_type: str | None = Field(default=None, max_length=50)
    section: str | None = Field(default=None, max_length=300)
    account_code: str | None = Field(default=None, max_length=32)


class KnownMappingIn(BaseModel):
    normalized_label: str = Field(max_length=500)
    category: Category
    sheet_type: str | None = Field(default=None, max_length=50)
    section: str | None = Field(default=None, max_length=300)


class SuggestRequest(BaseModel):
    accounts: list[AccountIn] = Field(max_length=5000)
    known_mappings: list[KnownMappingIn] = Field(default_factory=list, max_length=10000)


class AlternativeOut(BaseModel):
    category: Category
    confidence: float


class SuggestionOut(BaseModel):
    id: str | None
    label: str
    category: Category
    confidence: float = Field(ge=0, le=1)
    method: str
    alternatives: list[AlternativeOut]


class SuggestResponse(BaseModel):
    suggestions: list[SuggestionOut]
