﻿#version 460 core

in vec2 aPos;

out vec2 uv;

void main()
{
    uv = aPos;
    gl_Position = vec4(aPos, 0.0, 1.0);
}