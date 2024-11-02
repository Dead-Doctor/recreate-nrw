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

uniform sampler1D dataTilesTexture;
uniform float baseTerrainTileSize;
uniform sampler1D terrainTextureCenters;
uniform int terrainTilesPerLod;

const float playerIndicatorRadius = 8.0;
const float playerIndicatorAntiAliasingWidth = 1.2;

const float stripeThickness = 5.0;

const vec3 backgroundColor = vec3(0.15, 0.15, 0.15);
const vec3 dataGridFillColor = vec3(0.25, 0.25, 0.25);
const vec3 dataGridColor = vec3(0.4, 0.4, 0.4);
const vec3 loadedTilesColor = vec3(0.3, 0.5, 0.8);
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
    vec2 worldPositionFloored = floor(worldPosition);
    
    vec2 terrainDataUv = mod(worldPosition, 1000);
    float dataGrid = min(terrainDataUv.x / dFdxFine(worldPosition.x), terrainDataUv.y / dFdyFine(worldPosition.y)) > 1.0 ? 0.0 : 1.0;
    
    vec2 terrainDataPosition = (worldPosition - vec2(-346000, 5675000)) * vec2(1, -1);
    vec2 terrainDataTile = floor(terrainDataPosition / 1000.0);
    float dataTileAvailale = 0.0;
    int countDataTiles = textureSize(dataTilesTexture, 0);
    for (int i = 0; i < countDataTiles; i++) {
        vec2 tilePosition = texelFetch(dataTilesTexture, i, 0).xy;
        dataTileAvailale = terrainDataTile == tilePosition ? 1.0 : dataTileAvailale;
    }
    
    float activeTiles = 0.0;
    int terrainTextureLods = textureSize(terrainTextureCenters, 0);
    for (int lod = 0; lod < terrainTextureLods; lod++) {
        int size = 1 << lod;
        float tileSize = baseTerrainTileSize * size;
        vec2 index = floor(worldPositionFloored / tileSize);
        vec2 center = texelFetch(terrainTextureCenters, lod, 0).xy;
        float maxDistanceToCenter = (terrainTilesPerLod - 1.0) / 2.0;
        activeTiles += abs((index.x + 0.5) - center.x) <= maxDistanceToCenter && abs((index.y + 0.5) - center.y) <= maxDistanceToCenter ? 1.0 : 0.0;
    }
    float tileActive = activeTiles / terrainTextureLods * (int(floor((gl_FragCoord.x + gl_FragCoord.y) / stripeThickness)) % 2);
    
    vec3 color = backgroundColor;
    color = mix(color, dataGridFillColor, dataTileAvailale);
    color = mix(color, dataGridColor, dataGrid);
    color = mix(color, loadedTilesColor, tileActive);
    color = mix(color, playerIndicatorColor, calculatePlayerIndicator(worldPosition));
    FragColor = vec4(color, 1.0);
}