import type React from "react";
import { AbsoluteFill, Img, staticFile } from "remotion";
import { loadFont as loadAnton } from "@remotion/google-fonts/Anton";
import { loadFont as loadBangers } from "@remotion/google-fonts/Bangers";
import { loadFont as loadOswald } from "@remotion/google-fonts/Oswald";

const { fontFamily: anton } = loadAnton("normal", { weights: ["400"], subsets: ["latin"] });
const { fontFamily: bangers } = loadBangers("normal", { weights: ["400"], subsets: ["latin"] });
const { fontFamily: oswald } = loadOswald("normal", { weights: ["500", "700"], subsets: ["latin"] });

const RED = "#b42a28";
const RED_DARK = "#5a0c0c";
const REFLECT = "#e6e8c8";
const INK = "#141212";
const GREEN = "#28984a";

type Props = {
  readonly shout: boolean;
};

/** Трассер: светящаяся голова и затухающий хвост, летит справа налево. */
const Tracer: React.FC<{ x: number; y: number; length: number; angle: number }> = ({ x, y, length, angle }) => (
  <div
    style={{
      position: "absolute",
      left: x,
      top: y,
      width: length,
      height: 5,
      borderRadius: 3,
      transformOrigin: "0 50%",
      rotate: `${angle}deg`,
      background: "linear-gradient(to right, #fffbe8 0%, #ffd77a 18%, rgba(255,150,40,0.55) 55%, rgba(255,120,30,0) 100%)",
      boxShadow: "0 0 14px 3px rgba(255,170,60,0.55)",
    }}
  >
    <div
      style={{
        position: "absolute",
        left: -6,
        top: -4,
        width: 18,
        height: 13,
        borderRadius: "50%",
        background: "#ffffff",
        boxShadow: "0 0 18px 6px rgba(255,230,160,0.9)",
      }}
    />
  </div>
);

/** Вспышка выстрела у края кадра. */
const Flash: React.FC<{ x: number; y: number; size: number }> = ({ x, y, size }) => (
  <div
    style={{
      position: "absolute",
      left: x - size / 2,
      top: y - size / 2,
      width: size,
      height: size,
      borderRadius: "50%",
      background: "radial-gradient(circle, #fffbe0 0%, #ffcf5a 25%, rgba(255,120,20,0.6) 50%, rgba(255,80,0,0) 72%)",
    }}
  />
);

/** Знак первой помощи: зелёный крест на белой плашке. */
const FirstAid: React.FC<{ size: number; style?: React.CSSProperties }> = ({ size, style }) => (
  <div
    style={{
      width: size,
      height: size,
      borderRadius: size * 0.16,
      background: "#efece2",
      border: `${Math.max(3, size * 0.07)}px solid ${INK}`,
      position: "relative",
      boxSizing: "border-box",
      ...style,
    }}
  >
    <div style={{ position: "absolute", left: "38%", top: "14%", width: "24%", height: "72%", background: GREEN }} />
    <div style={{ position: "absolute", left: "14%", top: "38%", width: "72%", height: "24%", background: GREEN }} />
  </div>
);

export const Cover: React.FC<Props> = ({ shout }) => {
  return (
    <AbsoluteFill style={{ background: `radial-gradient(ellipse at 68% 55%, #8e1b1a 0%, #5a0c0c 45%, #250404 100%)`, overflow: "hidden" }}>
      {/* диагональные полосы, еле заметные */}
      <AbsoluteFill
        style={{
          background: "repeating-linear-gradient(-45deg, rgba(255,255,255,0.035) 0 34px, rgba(0,0,0,0) 34px 78px)",
        }}
      />

      {/* огромный знак за спиной */}
      <div style={{ position: "absolute", left: 640, top: 40, opacity: 0.1, rotate: "-8deg" }}>
        <div style={{ position: "relative", width: 620, height: 620 }}>
          <div style={{ position: "absolute", left: 220, top: 0, width: 180, height: 620, background: "#ffffff", borderRadius: 18 }} />
          <div style={{ position: "absolute", left: 0, top: 220, width: 620, height: 180, background: "#ffffff", borderRadius: 18 }} />
        </div>
      </div>

      {/* свет за медиком */}
      <div
        style={{
          position: "absolute",
          left: 700,
          top: 120,
          width: 560,
          height: 560,
          borderRadius: "50%",
          background: "radial-gradient(circle, rgba(255,214,170,0.45) 0%, rgba(255,160,120,0.12) 45%, rgba(0,0,0,0) 70%)",
        }}
      />

      {/* трассеры из-за правого края: мимо медика */}
      <Flash x={1290} y={250} size={130} />
      <Flash x={1290} y={600} size={130} />
      <Tracer x={730} y={262} length={300} angle={-4} />
      <Tracer x={1110} y={330} length={170} angle={-22} />
      <Tracer x={560} y={668} length={420} angle={-6} />

      {/* тень на земле */}
      <div
        style={{
          position: "absolute",
          left: 660,
          top: 640,
          width: 560,
          height: 70,
          borderRadius: "50%",
          background: "radial-gradient(ellipse, rgba(0,0,0,0.55) 0%, rgba(0,0,0,0) 70%)",
        }}
      />

      {/* раненый у ног */}
      <svg width="360" height="170" viewBox="0 0 360 170" style={{ position: "absolute", left: 650, top: 540 }}>
        {/* лужа крови под раненым */}
        <path
          d="M40 92 C20 60 70 34 120 40 C160 22 220 30 250 46 C300 38 345 62 330 96 C350 122 300 146 250 136 C215 158 160 150 130 138 C90 152 30 134 40 112 C20 104 26 96 40 92 Z"
          fill="#2a0202"
          stroke="#120000"
          strokeWidth="5"
        />
        <path
          d="M70 92 C60 70 100 56 140 60 C175 46 220 54 245 66 C285 62 310 80 300 100 C312 118 278 130 245 124 C215 138 170 134 145 126 C110 136 64 124 70 106 Z"
          fill="#6e0a0a"
        />
        <ellipse cx="200" cy="74" rx="50" ry="8" fill="rgba(255,170,170,0.45)" />
        <ellipse cx="120" cy="82" rx="20" ry="4" fill="rgba(255,180,180,0.35)" />
        {/* брызги */}
        <circle cx="18" cy="70" r="7" fill="#2a0202" stroke="#120000" strokeWidth="2" />
        <circle cx="342" cy="138" r="6" fill="#2a0202" stroke="#120000" strokeWidth="2" />
        <circle cx="300" cy="22" r="5" fill="#2a0202" stroke="#120000" strokeWidth="2" />
        <circle cx="60" cy="150" r="4" fill="#2a0202" stroke="#120000" strokeWidth="2" />
      </svg>
      <Img src={staticFile("wounded.png")} style={{ position: "absolute", left: 600, top: 470, width: 380, filter: "drop-shadow(0 6px 8px rgba(0,0,0,0.5))" }} />

      {/* медик */}
      <Img
        src={staticFile("medic.png")}
        style={{
          position: "absolute",
          left: 800,
          top: 40,
          width: 470,
          filter: "drop-shadow(0 0 2px rgba(255,235,210,0.9)) drop-shadow(0 0 22px rgba(255,200,160,0.45)) drop-shadow(0 18px 24px rgba(0,0,0,0.55))",
        }}
      />

      {/* крик */}
      {shout ? (
        <div style={{ position: "absolute", left: 712, top: 4, rotate: "-4deg", scale: "0.84", transformOrigin: "0 0" }}>
          <svg width="470" height="250" viewBox="0 0 470 250" style={{ position: "absolute", left: 0, top: 0, overflow: "visible" }}>
            <path
              d="M40 10 H430 Q460 10 460 40 V150 Q460 180 430 180 H330 L372 248 L262 180 H40 Q10 180 10 150 V40 Q10 10 40 10 Z"
              fill="#ffffff"
              stroke={INK}
              strokeWidth="9"
              strokeLinejoin="round"
            />
          </svg>
          <div style={{ position: "relative", width: 470, height: 190, display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center" }}>
            <div style={{ fontFamily: bangers, fontSize: 92, lineHeight: 0.95, color: RED, letterSpacing: 2, textShadow: `3px 3px 0 ${RED_DARK}` }}>MEDIC!</div>
            <div style={{ fontFamily: bangers, fontSize: 50, lineHeight: 1, color: INK, letterSpacing: 2 }}>HOLD YOUR FIRE!</div>
          </div>
        </div>
      ) : null}

      {/* левая колонка: название */}
      <div
        style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: 620,
          height: 720,
          background: "linear-gradient(to right, rgba(20,4,4,0.82) 0%, rgba(20,4,4,0.55) 70%, rgba(20,4,4,0) 100%)",
        }}
      />
      <div style={{ position: "absolute", left: 60, top: 70 }}>
        <FirstAid size={104} style={{ boxShadow: "0 8px 20px rgba(0,0,0,0.5)" }} />
      </div>
      <div
        style={{
          position: "absolute",
          left: 52,
          top: 196,
          fontFamily: anton,
          fontSize: 150,
          lineHeight: 0.95,
          color: "#f4f1e8",
          letterSpacing: 1,
          WebkitTextStroke: `3px ${INK}`,
          textShadow: `0 6px 0 ${RED_DARK}, 0 12px 26px rgba(0,0,0,0.6)`,
        }}
      >
        PARAMEDIC
      </div>
      <div
        style={{
          position: "absolute",
          left: 58,
          top: 360,
          fontFamily: bangers,
          fontSize: 74,
          lineHeight: 1,
          color: "#ffd45a",
          letterSpacing: 3,
          rotate: "-2deg",
          textShadow: `3px 4px 0 ${INK}`,
        }}
      >
        DON'T DIE YET!
      </div>

      {/* светоотражающая лента */}
      <div style={{ position: "absolute", left: 58, top: 468, width: 470, height: 18, background: REFLECT, borderTop: "3px solid #9a9c86", borderBottom: "3px solid #9a9c86" }} />

      <div style={{ position: "absolute", left: 60, top: 512, fontFamily: oswald, fontWeight: 500, fontSize: 30, lineHeight: 1.45, color: "#f1ece0", textShadow: "0 2px 6px rgba(0,0,0,0.7)" }}>
        <div>Wear the uniform. Save lives.</div>
        <div>
          <span style={{ color: "#ffd45a", fontWeight: 700 }}>8</span> uniform pieces · <span style={{ color: "#ffd45a", fontWeight: 700 }}>3</span> abilities
        </div>
        <div>And an oath not to kill.</div>
      </div>

      {/* виньетка */}
      <AbsoluteFill style={{ background: "radial-gradient(ellipse at center, rgba(0,0,0,0) 60%, rgba(0,0,0,0.45) 100%)" }} />
    </AbsoluteFill>
  );
};
