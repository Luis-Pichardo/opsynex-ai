"""Dobles de prueba: ninguna prueba llama a la API real de Claude."""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any

from pydantic import BaseModel

from app.llm.client import LlmResult, LlmUsage


@dataclass
class FakeLlm:
    """Cliente de IA falso: devuelve respuestas fijas y guarda los prompts recibidos."""

    responses: dict[type, BaseModel] = field(default_factory=dict)
    calls: list[dict[str, Any]] = field(default_factory=list)
    error: Exception | None = None

    async def generate(
        self, *, system: str, user: str, output: type, purpose: str, max_tokens: int = 16000
    ) -> LlmResult:
        self.calls.append({"system": system, "user": user, "output": output, "purpose": purpose})
        if self.error is not None:
            raise self.error
        return LlmResult(data=self.responses[output], model="claude-test", usage=LlmUsage(10, 5))


class FakeUsage:
    def __init__(self) -> None:
        self.input_tokens = 1200
        self.output_tokens = 350
        self.cache_creation_input_tokens = 0
        self.cache_read_input_tokens = 900


class FakeResponse:
    def __init__(
        self, parsed: Any, stop_reason: str = "end_turn", stop_details: Any = None
    ) -> None:
        self.parsed_output = parsed
        self.stop_reason = stop_reason
        self.stop_details = stop_details
        self.model = "claude-opus-5-5"
        self.usage = FakeUsage()


class FakeMessages:
    def __init__(
        self, response: FakeResponse | None = None, error: Exception | None = None
    ) -> None:
        self.response = response
        self.error = error
        self.kwargs: dict[str, Any] | None = None

    async def parse(self, **kwargs: Any) -> FakeResponse:
        self.kwargs = kwargs
        if self.error is not None:
            raise self.error
        assert self.response is not None
        return self.response


class FakeSdk:
    """Imita `anthropic.AsyncAnthropic` en lo que usa `ClaudeClient` (`beta.messages.parse`)."""

    def __init__(
        self, response: FakeResponse | None = None, error: Exception | None = None
    ) -> None:
        self.messages = FakeMessages(response, error)

        class _Beta:
            pass

        self.beta = _Beta()
        self.beta.messages = self.messages  # type: ignore[attr-defined]
