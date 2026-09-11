/**
 * WebGL2 minimo para temas cinema/imersivo — chunk separado, sem three no portal/studio.
 * Orçamento: deve caber em ≤ 200 kB gzip (RN-FRT-001).
 */
export function mountImmersive(host: HTMLElement, textureUrls: string[]) {
  const canvas = document.createElement("canvas");
  canvas.className = "h-full w-full opacity-40";
  host.replaceChildren(canvas);

  const gl = canvas.getContext("webgl2");
  if (!gl) return;

  const resize = () => {
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    canvas.width = Math.floor(host.clientWidth * dpr);
    canvas.height = Math.floor(host.clientHeight * dpr);
    gl.viewport(0, 0, canvas.width, canvas.height);
  };
  resize();
  window.addEventListener("resize", resize);

  const vs = `#version 300 es
  in vec2 a; void main(){ gl_Position = vec4(a,0.,1.); }`;
  const fs = `#version 300 es
  precision mediump float; out vec4 o; uniform float t;
  void main(){ float g = .15 + .05*sin(t); o = vec4(g, g*.9, g*.8, 1.); }`;

  const compile = (type: number, src: string) => {
    const s = gl.createShader(type)!;
    gl.shaderSource(s, src);
    gl.compileShader(s);
    return s;
  };
  const prog = gl.createProgram()!;
  gl.attachShader(prog, compile(gl.VERTEX_SHADER, vs));
  gl.attachShader(prog, compile(gl.FRAGMENT_SHADER, fs));
  gl.linkProgram(prog);
  gl.useProgram(prog);

  const buf = gl.createBuffer();
  gl.bindBuffer(gl.ARRAY_BUFFER, buf);
  gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1, -1, 3, -1, -1, 3]), gl.STATIC_DRAW);
  const loc = gl.getAttribLocation(prog, "a");
  gl.enableVertexAttribArray(loc);
  gl.vertexAttribPointer(loc, 2, gl.FLOAT, false, 0, 0);
  const uT = gl.getUniformLocation(prog, "t");

  // textures reservadas para a proxima iteracao (URLs ja autenticadas).
  void textureUrls;

  let raf = 0;
  const start = performance.now();
  const frame = (now: number) => {
    gl.uniform1f(uT, (now - start) / 1000);
    gl.drawArrays(gl.TRIANGLES, 0, 3);
    raf = requestAnimationFrame(frame);
  };
  raf = requestAnimationFrame(frame);

  return () => {
    cancelAnimationFrame(raf);
    window.removeEventListener("resize", resize);
  };
}
