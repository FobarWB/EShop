// The settings in /config.json: they differ per environment, so they are read at start, not compiled in.
export interface IClientConfig
{
  authority: string;
  clientId: string;
}
