"""ClaudeClient con un SDK simulado: parámetros, stop_reason, errores por tipo y uso de tokens."""

import logging

import anthropic
import httpx2
import pytest

from app.config import Settings
from app.llm.client import FALLBACK_BETA, ClaudeClient, LlmError
from app.schemas.insights import SummaryDraft
from tests.fakes import FakeResponse, FakeSdk

pytestmark = pytest.mark.anyio

REQUEST = httpx2.Request("POST", "https://api.anthropic.com/v1/messages")
DRAFT = SummaryDraft(paragraphs=["Uno.", "Dos.", "Tres."], priorities=["A", "B", "C"])


def settings(**overrides: object) -> Settings:
    values = {"ANTHROPIC_API_KEY": "sk-test", "ANTHROPIC_MODEL": "claude-opus-5-5"}
    values.update({k: str(v) for k, v in overrides.items()})
    return Settings(**values)  # type: ignore[arg-type]


async def generate(client: ClaudeClient, user: str = "Datos agregados: margen 18,6 %") -> object:
    return await client.generate(
        system="Instrucciones fijas", user=user, output=SummaryDraft, purpose="test"
    )


def status_error(cls: type[anthropic.APIStatusError], status: int) -> anthropic.APIStatusError:
    return cls("error", response=httpx2.Response(status, request=REQUEST), body=None)


async def test_sends_structured_output_cached_system_effort_and_fallbacks(caplog) -> None:
    sdk = FakeSdk(FakeResponse(DRAFT))
    caplog.set_level(logging.INFO, logger="opsynex.llm")

    result = await generate(ClaudeClient(settings(), sdk))

    kwargs = sdk.messages.kwargs
    assert kwargs is not None
    assert kwargs["model"] == "claude-opus-5-5"
    assert kwargs["output_format"] is SummaryDraft
    assert kwargs["output_config"] == {"effort": "medium"}
    assert kwargs["system"] == [
        {"type": "text", "text": "Instrucciones fijas", "cache_control": {"type": "ephemeral"}}
    ]
    assert kwargs["messages"][0]["role"] == "user"
    assert kwargs["fallbacks"] == "default"
    assert kwargs["betas"] == [FALLBACK_BETA]
    assert "thinking" not in kwargs  # Opus 5.5: pensamiento adaptativo por defecto
    assert result.data == DRAFT
    assert result.usage.cache_read_input_tokens == 900
    assert "input=1200 output=350 cache_write=0 cache_read=900" in caplog.text


async def test_fallbacks_can_be_disabled_for_models_without_them() -> None:
    sdk = FakeSdk(FakeResponse(DRAFT))
    await generate(
        ClaudeClient(settings(ANTHROPIC_FALLBACKS="none", ANTHROPIC_MODEL="claude-haiku-5-5"), sdk)
    )
    assert "fallbacks" not in sdk.messages.kwargs
    assert "betas" not in sdk.messages.kwargs


@pytest.mark.parametrize(
    ("response", "code"),
    [
        (FakeResponse(None, "refusal"), "llm_refused"),
        (FakeResponse(None, "max_tokens"), "llm_truncated"),
        (FakeResponse(None, "end_turn"), "llm_invalid_output"),
    ],
)
async def test_checks_stop_reason_before_reading_content(response: FakeResponse, code: str) -> None:
    with pytest.raises(LlmError) as error:
        await generate(ClaudeClient(settings(), FakeSdk(response)))
    assert error.value.code == code


async def test_refusal_mentions_the_category() -> None:
    class Details:
        category = "cyber"

    with pytest.raises(LlmError) as error:
        await generate(ClaudeClient(settings(), FakeSdk(FakeResponse(None, "refusal", Details()))))
    assert error.value.status == 422
    assert "(cyber)" in error.value.message


@pytest.mark.parametrize(
    ("exception", "code", "status"),
    [
        (status_error(anthropic.RateLimitError, 429), "llm_rate_limited", 429),
        (status_error(anthropic.InternalServerError, 500), "llm_upstream_error", 502),
        (status_error(anthropic.AuthenticationError, 401), "llm_not_configured", 503),
        (status_error(anthropic.NotFoundError, 404), "llm_bad_model", 503),
        (status_error(anthropic.BadRequestError, 400), "llm_bad_request", 502),
        (anthropic.APIConnectionError(request=REQUEST), "llm_unavailable", 503),
        (anthropic.APITimeoutError(request=REQUEST), "llm_timeout", 504),
    ],
)
async def test_maps_sdk_errors_by_type(exception: Exception, code: str, status: int) -> None:
    with pytest.raises(LlmError) as error:
        await generate(ClaudeClient(settings(), FakeSdk(error=exception)))
    assert (error.value.code, error.value.status) == (code, status)


async def test_blocks_personal_data_before_calling_claude() -> None:
    sdk = FakeSdk(FakeResponse(DRAFT))
    with pytest.raises(LlmError) as error:
        await generate(ClaudeClient(settings(), sdk), user="EMPLEADO 001, cédula 001-1234567-8")
    assert error.value.code == "llm_personal_data_blocked"
    assert sdk.messages.kwargs is None  # nunca llegó al SDK
