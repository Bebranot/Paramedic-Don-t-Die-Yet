import { Still } from "remotion";
import { Cover } from "./Composition";

export const RemotionRoot: React.FC = () => {
  return (
    <>
      <Still id="Cover" component={Cover} width={1280} height={720} defaultProps={{ shout: true }} />
      <Still id="CoverNoShout" component={Cover} width={1280} height={720} defaultProps={{ shout: false }} />
    </>
  );
};
