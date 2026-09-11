export default function StudioHomePage() {
  return (
    <main className="mx-auto max-w-3xl px-6 py-16">
      <h1 className="text-2xl font-medium">Studio</h1>
      <p className="mt-2 text-neutral-600">
        Back-office do fotografo — CSR. Sem WebGL, sem scroll hijack.
      </p>
      <p className="mt-6 flex flex-col gap-2">
        <a className="underline" href="/studio/orders">
          Ir para pedidos
        </a>
        <a className="underline" href="/studio/briefing">
          Editor de perguntas do briefing
        </a>
      </p>
    </main>
  );
}
