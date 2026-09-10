"""
Gemini API wrapper for image generation, editing, and analysis.
API key loaded from GEMINI_API_KEY environment variable.
"""

import os
import logging
from google import genai
from google.genai import types

logger = logging.getLogger(__name__)

GEMINI_API_KEY = os.environ.get("GEMINI_API_KEY", "")

MODEL_ALIASES = {
    "nano-banana": "gemini-2.0-flash-exp-image-generation",
    "nano-banana-pro": "gemini-3-pro-image-preview",
}


def _get_client() -> genai.Client:
    """Create a Gemini client using the API key from environment."""
    if not GEMINI_API_KEY:
        raise RuntimeError("GEMINI_API_KEY not set")
    return genai.Client(api_key=GEMINI_API_KEY)


def _resolve_model(model: str) -> str:
    """Resolve a model alias to its actual Gemini model string."""
    return MODEL_ALIASES.get(model, model)


def generate_image(prompt: str, model: str = "nano-banana", aspect_ratio: str = "1:1") -> dict:
    """
    Generate an image from a text prompt.
    Returns dict with keys: image_bytes, mime_type, model_used.
    Raises RuntimeError on API failure or if no image is returned.
    """
    client = _get_client()
    model_str = _resolve_model(model)

    # Not all models support aspect_ratio — only include if non-default
    image_cfg_kwargs = {}
    if aspect_ratio and aspect_ratio != "1:1":
        image_cfg_kwargs["aspect_ratio"] = aspect_ratio

    response = client.models.generate_content(
        model=model_str,
        contents=prompt,
        config=types.GenerateContentConfig(
            response_modalities=["IMAGE", "TEXT"],
            image_config=types.ImageConfig(**image_cfg_kwargs),
        ),
    )

    for part in response.candidates[0].content.parts:
        if part.inline_data and part.inline_data.mime_type.startswith("image/"):
            return {
                "image_bytes": part.inline_data.data,
                "mime_type": part.inline_data.mime_type,
                "model_used": model_str,
            }

    raise RuntimeError("Gemini returned no image in the response")


def edit_image(source_bytes: bytes, prompt: str, model: str = "nano-banana") -> dict:
    """
    Edit an existing image with a text prompt.
    Returns dict with keys: image_bytes, mime_type, model_used.
    """
    client = _get_client()
    model_str = _resolve_model(model)

    response = client.models.generate_content(
        model=model_str,
        contents=[
            types.Part.from_bytes(data=source_bytes, mime_type="image/png"),
            prompt,
        ],
        config=types.GenerateContentConfig(
            response_modalities=["IMAGE", "TEXT"],
            image_config=types.ImageConfig(),
        ),
    )

    for part in response.candidates[0].content.parts:
        if part.inline_data and part.inline_data.mime_type.startswith("image/"):
            return {
                "image_bytes": part.inline_data.data,
                "mime_type": part.inline_data.mime_type,
                "model_used": model_str,
            }

    raise RuntimeError("Gemini returned no image in the edit response")


def analyze_image(image_bytes: bytes, prompt: str = "Describe this image in detail") -> dict:
    """
    Analyze an image and return a text description.
    Returns dict with keys: text, model_used.
    """
    client = _get_client()
    model_str = "gemini-2.0-flash"

    response = client.models.generate_content(
        model=model_str,
        contents=[
            types.Part.from_bytes(data=image_bytes, mime_type="image/png"),
            prompt,
        ],
    )

    text = response.candidates[0].content.parts[0].text
    return {
        "text": text,
        "model_used": model_str,
    }
