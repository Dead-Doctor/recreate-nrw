#version 460 core

#define PI 3.1415926538

in vec2 uv;

out vec4 FragColor;

uniform float frameHeight;
uniform float aspectRatio;
uniform vec2 position;
uniform float size;

uniform vec2 playerPosition;
uniform float playerDirection;

const float terrainTileSize = 2048.0;
const float playerIndicatorRadius = 8.0;
const float playerIndicatorAntiAliasingWidth = 1.2;

const vec3 backgroundColor = vec3(0.15, 0.15, 0.15);
const vec3 dataGridColor = vec3(0.4, 0.4, 0.4);
const vec3 tileGridColor = vec3(0.3, 0.5, 0.8);
const vec3 playerIndicatorColor = vec3(0.2, 0.3, 5.0);

float calculatePlayerIndicator(vec2 worldPosition) {
    vec2 offsetInPixel = (worldPosition - playerPosition) / size * frameHeight / 2.0;
    float indicatorCircle = 1.0 - clamp((length(offsetInPixel) - playerIndicatorRadius) / playerIndicatorAntiAliasingWidth, 0.0, 1.0);
    
    vec2 directionVec = vec2(sin(playerDirection), -cos(playerDirection));
    vec2 directionLeftVec = vec2(sin(playerDirection + PI / 4.0), -cos(playerDirection + PI / 4.0));
    vec2 directionRightVec = vec2(sin(playerDirection - PI / 4.0), -cos(playerDirection - PI / 4.0));
    
    vec2 offsetSquareCenter = offsetInPixel - directionVec * playerIndicatorRadius / sqrt(2.0);
    
    float distanceSquare = max(abs(dot(offsetSquareCenter, directionRightVec)), abs(dot(offsetSquareCenter, directionLeftVec)));
    float indicatorSquare = 1.0 - clamp((distanceSquare - playerIndicatorRadius / 2.0) / playerIndicatorAntiAliasingWidth, 0.0, 1.0); 
    
    return max(indicatorCircle, indicatorSquare);
}

void main() {
    vec2 offset = uv * vec2(size * aspectRatio, size);
    vec2 worldPosition = position + offset;

    vec2 terrainDataUv = mod(worldPosition, 1000);
    float dataGrid = min(terrainDataUv.x / dFdxFine(worldPosition.x), terrainDataUv.y / dFdyFine(worldPosition.y)) > 1.0 ? 0.0 : 1.0;
    vec2 terrainTileUv = mod(worldPosition, 2048);
    float tileGrid = min(terrainTileUv.x / dFdxFine(worldPosition.x), terrainTileUv.y / dFdyFine(worldPosition.y)) > 1.0 ? 0.0 : 1.0;
    
    vec3 backgroundMix = backgroundColor;
    vec3 dataGridMix = mix(backgroundMix, dataGridColor, dataGrid);
    vec3 tileGridMix = mix(dataGridMix, tileGridColor, tileGrid);
    vec3 indicatorMix = mix(tileGridMix, playerIndicatorColor, calculatePlayerIndicator(worldPosition));
    FragColor = vec4(indicatorMix, 1.0);
}